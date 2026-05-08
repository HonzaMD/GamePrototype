using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Assets.Scripts.Map.CellSims
{
    public sealed class CellSimWorld : IDisposable
    {
        public readonly Map Map;
        public readonly int Width;
        public readonly int Height;

        public NativeArray<byte> material;

        // Element field — diskrétní hmota přes Margolus block rule.
        // 3-buffer model: gameplay R/W přes elementPublic, sim si snapshotne Public→Front,
        // pojede Front→Back a po stepu reconcileuje: Public += Back - Front.
        public NativeArray<sbyte> elementPublic;
        private NativeArray<sbyte> elementFront;
        private NativeArray<sbyte> elementBack;

        public bool InitMode { get; private set; } = true;
        public JobHandle Pending;

        private int stepCounter;

        public CellSimWorld(Map map, int width, int height)
        {
            Map = map;
            Width = width;
            Height = height;
            int n = width * height;
            material = new NativeArray<byte>(n, Allocator.Persistent);
            elementPublic = new NativeArray<sbyte>(n, Allocator.Persistent);
            elementFront = new NativeArray<sbyte>(n, Allocator.Persistent);
            elementBack = new NativeArray<sbyte>(n, Allocator.Persistent);
        }

        public int Idx(int x, int y) => y * Width + x;
        public int Idx(Vector2Int pos) => pos.y * Width + pos.x;

        // ---- Element R/W API (Vector2Int souřadnice, bounds-checked, clamped) ----

        public int GetElement(Vector2Int pos)
        {
            if ((uint)pos.x >= (uint)Width || (uint)pos.y >= (uint)Height) return 0;
            int v = elementPublic[pos.y * Width + pos.x];
            return v < 0 ? 0 : v;
        }

        public void SetElement(Vector2Int pos, int value)
        {
            if ((uint)pos.x >= (uint)Width || (uint)pos.y >= (uint)Height) return;
            elementPublic[pos.y * Width + pos.x] = (sbyte)math.clamp(value, sbyte.MinValue, sbyte.MaxValue);
        }

        public void AddElement(Vector2Int pos, int delta)
        {
            if ((uint)pos.x >= (uint)Width || (uint)pos.y >= (uint)Height) return;
            int idx = pos.y * Width + pos.x;
            int v = elementPublic[idx] + delta;
            elementPublic[idx] = (sbyte)math.clamp(v, sbyte.MinValue, sbyte.MaxValue);
        }

        // ---- Lifecycle ----

        public void EndInit()
        {
            Pending.Complete();
            InitMode = false;
        }

        public void Step()
        {
            if (InitMode) return;

            int n = Width * Height;

            // 1. Sync část — blokuje main thread, ale jen krátce.
            //    a) Dokončíme Margolus z minulého stepu (Back má sim výstup, Front starý snapshot).
            //    b) Reconcile + nový snapshot v jednom průchodu:
            //       Pub_new = Pub + (Back - Front), Front_new = Pub_new.
            //    Po Complete() je Public konsolidovaný a gameplay s ním zase může pracovat.
            Pending.Complete();
            new CellSimReconcileSnapshotSByteJob
            {
                Back = elementBack,
                Front = elementFront,
                Pub = elementPublic,
            }.Schedule(n, 1024).Complete();

            // 2. Async část — main thread je volný, gameplay může R/W elementPublic.
            //    a) Init Back = Front (border cells netknuté Margolusem mají v příštím
            //       reconcile delta=0, když Width/Height nedělitelné offsetem → 1-cell pruh).
            //    b) Margolus block step.
            var initBack = new CellSimCopySByteJob { Src = elementFront, Dst = elementBack }
                .Schedule(n, 1024);

            int2 offset = (stepCounter & 1) == 0 ? new int2(0, 0) : new int2(1, 1);
            int blocksX = (Width - offset.x) / 2;
            int blocksY = (Height - offset.y) / 2;
            int blockCount = blocksX * blocksY;

            JobHandle margolus = new ElementMargolusJob
            {
                EFront = elementFront,
                EBack = elementBack,
                Width = Width,
                BlocksX = blocksX,
                Offset = offset,
                StepMod4 = stepCounter & 3,
            }.Schedule(blockCount, 64, initBack);

            Pending = margolus;
            stepCounter++;
        }

        public void Dispose()
        {
            Pending.Complete();
            if (material.IsCreated) material.Dispose();
            if (elementPublic.IsCreated) elementPublic.Dispose();
            if (elementFront.IsCreated) elementFront.Dispose();
            if (elementBack.IsCreated) elementBack.Dispose();
        }
    }
}
