using Assets.Scripts.Map;
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Objekty s aktivnim RB, ktere RefreshMapPositions promita do mapy (a zabiji ty, co vypadly daleko z mapy).
    /// SoA: entries, transforms, lastPivot a moved maji stejny index; Placeable.MovingIndex ukazuje do nich.
    /// Odebirani je swap-back. Mezi BeginStepAndFindMoved a EndStep se odklada (indexy se nesmi posunout pod iteraci).
    /// </summary>
    public class MovingObjects : IDisposable
    {
        public struct Entry
        {
            public Placeable Obj;       // null = odebrano behem kroku, zahodi se v EndStep
            public Map.Map Map;
            public int Tag;             // skupina round robinu slowPass
            public bool AlwaysMapMove;  // zrcadlo Placeable.AlwaysMapMove, at iterace nesaha na objekt
        }

        // pocitadla RefreshMapPositions, loguji se ve FastMode jednou za sekundu
        private struct Stats
        {
            public long Steps, Objects, SlowPass, Always, Moved, Killed;
        }

        public const int TagCount = 20;
        private const float KillDistance = 50; // m od okraje mapy, ve vsech smerech
        private const float MoveThresholdSq = 0.1f * 0.1f;
        private const int InitialCapacity = 1024;
        private const int JobBatchSize = 64;

        private Entry[] entries = new Entry[InitialCapacity];
        private int count;
        private TransformAccessArray transforms = new(InitialCapacity);
        private NativeArray<float2> lastPivot = new(InitialCapacity, Allocator.Persistent);   // PlacedPosition - PosOffset pri poslednim Move
        private NativeArray<byte> moved = new(InitialCapacity, Allocator.Persistent);
        private readonly List<int> pendingRemoves = new();
        private bool inStep;
        private int inserterTag;
        private int workTag;
        private int slowPassFrame = -1;
        private Stats stats;

        public int Count => count;

        public void Add(Placeable p, Map.Map map)
        {
            int tag = inserterTag++;
            if (inserterTag >= TagCount)
                inserterTag = 0;

            if (p.MovingIndex >= 0)
            {
                ref var e = ref entries[p.MovingIndex];
                e.Map = map;
                e.Tag = tag;
                e.AlwaysMapMove = p.AlwaysMapMove;
                return;
            }

            if (count == entries.Length)
                Grow();

            entries[count] = new Entry { Obj = p, Map = map, Tag = tag, AlwaysMapMove = p.AlwaysMapMove };
            lastPivot[count] = p.PlacedPosition - p.PosOffset;
            transforms.Add(p.transform);
            p.MovingIndex = count;
            count++;
        }

        public void Remove(Placeable p)
        {
            int index = p.MovingIndex;
            if (index < 0)
                return;
            p.MovingIndex = -1;

            if (inStep)
            {
                entries[index].Obj = null;
                pendingRemoves.Add(index);
            }
            else
            {
                RemoveAtSwapBack(index);
            }
        }

        public void RefreshAlwaysMapMove(Placeable p)
        {
            if (p.MovingIndex >= 0)
                entries[p.MovingIndex].AlwaysMapMove = p.AlwaysMapMove;
        }

        /// <summary>
        /// Promitne pozice do mapy. Vola se po kazde Physics.Simulate (Game.MapRefreshLoop), viz Docs/map-update-timing.md.
        /// Job oznaci posunute objekty; na ostatni (vetsinu) se vubec nesaha.
        /// </summary>
        public void RefreshMapPositions()
        {
            // round robin (plny Move + MovingObjTest) jen v prvni simulaci framu -> perioda v framech
            bool slowPass = slowPassFrame != Time.frameCount;
            slowPassFrame = Time.frameCount;

            // polozky pridane behem kroku (za stepCount) nemaji vysledek jobu, zpracuji se v dalsim kroku
            int stepCount = BeginStepAndFindMoved();
            try
            {
                for (int i = 0; i < stepCount; i++)
                {
                    var e = entries[i];
                    bool slow = slowPass && e.Tag == workTag;
                    // ReferenceEquals: jen odebrane behem kroku; Unity == by sahal do kazdeho objektu
                    if (ReferenceEquals(e.Obj, null) || !(slow || e.AlwaysMapMove || moved[i] != 0))
                        continue;

                    var obj = e.Obj;
                    try
                    {
                        if (slow)
                        {
                            // Kill odregistruje (pod krokem odlozene), indexy se do EndStep neposunou
                            if (e.Map.IsFarOutside(obj.PlacedPosition, KillDistance))
                            {
                                obj.Kill();
                                stats.Killed++;
                                continue;
                            }
                            e.Map.Move(obj);
                            MovingObjTest(obj, e.Map);
                            stats.SlowPass++;
                        }
                        else
                        {
                            e.Map.Move(obj);
                            if (e.AlwaysMapMove)
                                stats.Always++;
                            else
                                stats.Moved++;
                        }
                        RememberPivotAfterMove(i);
                    }
                    catch (Exception ex)
                    {
                        // vyjimka mimo smycku by ukoncila coroutinu a mapa by se prestala obnovovat
                        Debug.LogException(ex);
                    }
                }
            }
            finally
            {
                EndStep();
            }
            stats.Steps++;
            stats.Objects += stepCount;

            if (slowPass)
            {
                workTag++;
                if (workTag >= TagCount)
                    workTag = 0;
            }
        }

        private static void MovingObjTest(Placeable p, Map.Map map)
        {
            if (Game.Instance.Ksids.IsParentOrEqual(p.Ksid, Ksid.SandLike) && p.IsNonMoving)
            {
                float y = Mathf.Repeat(p.Pivot.y, Map.Map.CellSize.y);
                float dy = p.PosOffset.y + p.Size.y;

                if (y + dy > Map.Map.CellSizeY3div4)
                    map.AddCellStateTest(map.WorldToCell(p.Pivot), p.CellZ == 0 ? CellStateCahnge.CompactSand0 : CellStateCahnge.CompactSand1);
            }
        }

        public void LogStats()
        {
            var s = stats;
            if (s.Steps > 0)
            {
                float steps = s.Steps;
                Debug.Log($"MapRefresh per step: objects {s.Objects / steps:F0}, slowPass {s.SlowPass / steps:F1}, always {s.Always / steps:F1}, moved {s.Moved / steps:F1}, killed {s.Killed}");
            }
            stats = default;
        }

        public void ResetStats() => stats = default;

        // Paralelne precte pozice a oznaci objekty posunute o vic nez prah.
        private int BeginStepAndFindMoved()
        {
            new FindMovedJob { LastPivot = lastPivot, Moved = moved, ThresholdSq = MoveThresholdSq }
                .ScheduleReadOnly(transforms, JobBatchSize)
                .Complete();
            inStep = true;
            return count;
        }

        // Po map.Move: novy referencni bod pro test posunu.
        private void RememberPivotAfterMove(int index)
        {
            var p = entries[index].Obj;
            if (!ReferenceEquals(p, null))
                lastPivot[index] = p.PlacedPosition - p.PosOffset;
        }

        private void EndStep()
        {
            inStep = false;
            if (pendingRemoves.Count == 0)
                return;

            // sestupne: na misto odebraneho se presune posledni, ktery uz neni odlozeny (vyssi indexy jsou pryc)
            pendingRemoves.Sort();
            for (int i = pendingRemoves.Count - 1; i >= 0; i--)
                RemoveAtSwapBack(pendingRemoves[i]);
            pendingRemoves.Clear();
        }

        private void RemoveAtSwapBack(int index)
        {
            int last = count - 1;
            if (index != last)
            {
                entries[index] = entries[last];
                lastPivot[index] = lastPivot[last];
                entries[index].Obj.MovingIndex = index;
            }
            entries[last] = default;
            transforms.RemoveAtSwapBack(index);
            count--;
        }

        private void Grow()
        {
            int capacity = entries.Length * 2;
            Array.Resize(ref entries, capacity);
            lastPivot = Resize(lastPivot, capacity);
            moved = Resize(moved, capacity); // muze rust behem kroku, vysledky jobu se musi zachovat
            transforms.capacity = capacity;
        }

        private static NativeArray<T> Resize<T>(NativeArray<T> old, int capacity) where T : struct
        {
            var n = new NativeArray<T>(capacity, Allocator.Persistent);
            NativeArray<T>.Copy(old, n, old.Length);
            old.Dispose();
            return n;
        }

        public void Dispose()
        {
            if (transforms.isCreated)
                transforms.Dispose();
            if (lastPivot.IsCreated)
                lastPivot.Dispose();
            if (moved.IsCreated)
                moved.Dispose();
        }

        [BurstCompile]
        private struct FindMovedJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<float2> LastPivot;
            [WriteOnly] public NativeArray<byte> Moved;
            public float ThresholdSq;

            public void Execute(int index, TransformAccess transform)
            {
                if (!transform.isValid)
                {
                    Moved[index] = 0;
                    return;
                }
                Vector3 pos = transform.position;
                Moved[index] = (byte)(math.distancesq(new float2(pos.x, pos.y), LastPivot[index]) > ThresholdSq ? 1 : 0);
            }
        }
    }
}
