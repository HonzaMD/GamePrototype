﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Map.Visibility
{

    internal delegate void NeighbourTest(Vector2Int pos, ref Cell cell);

    public class VCore
    {
        public const int HalfXSize = 32;
        public const int HalfYSize = 26;
        private static readonly Vector2Int centerCellLocal = new (HalfXSize, HalfYSize);
        public static readonly float ShadowRadius = ((Vector2)centerCellLocal * 0.5f).magnitude * 1.2f;
        public const float ShadowFrontZ = -1.30f;
        public const float ShadowBackZ = 1.80f;

        private const int sizeX = HalfXSize * 2 + 1;
        private const int sizeY = HalfYSize * 2 + 1;

        internal const float centerRadius = 0.7f;
        internal const float centerRadiusMarginSq = centerRadius * centerRadius * 1.8f * 1.8f;

        private readonly Cell[] vmap = new Cell[sizeY * sizeX];
        private readonly byte[] seedsMap = new byte[sizeY * sizeX];
        private Map map;
        internal Vector2 centerPosLocal; // pozice stedu mapy
        private Vector2Int cellToWorld; // Pozice nulte bunky v souradniciuch vnejsiho sveta
        internal Vector2 posToWorld;

        private readonly Action<Vector2Int> createDarkSeedsAction;
        
        private readonly Queue<int>[] ffvQueue = new Queue<int>[5];
        private int ffvQueueCount;
        private int ffvDistance;
        private const int ffvMaxDistance = 10 * 3;

        private readonly ShadowWorker shadowWorker;
        private readonly DCManager dcManager;
        private readonly DarkBorders darkBorders;

        private readonly Stopwatch swFloodFillVisible = new();
        private readonly Stopwatch swCastShadows = new();
        private readonly Stopwatch swCreateSeeds = new();
        private readonly Stopwatch swDrawDarks = new();
        private readonly Stopwatch swCreateDcs = new();
        private readonly Stopwatch swBuildMesh = new();
        private int castShadowCounter;
        private int dSeedTestCounter;
        private int resolveShadowCounter;

        public VCore()
        {
            shadowWorker = new(this);
            darkBorders = new();
            dcManager = new(this, darkBorders);
            createDarkSeedsAction = CreateDarkSeeds;
            for (int i = 0; i < ffvQueue.Length; i++)
                ffvQueue[i] = new();
        }

        public void Compute(Vector2 center, Map map)
        {
            this.map = map;
            CalcCoordinateOffsets(center);

            Reset();

            CastShadows();
            FloodFillVisible();

            int maxCircles = Math.Max(HalfYSize, HalfXSize);

            for (int cc = 2; cc < maxCircles; cc++)
            {
                DrawDarks(cc);
                DoCircle(cc - 1, createDarkSeedsAction, swCreateSeeds, new Vector2Int(HalfXSize - 2, HalfYSize - 2));
                if (dcManager.TryCastDark(swCreateDcs))
                    DrawDarks(cc);
            }
            dcManager.TryCastDark(swCreateDcs);

            BuildMeshes();
            //DrawDebug();
            //DrawDebugOne();
        }


        private void CalcCoordinateOffsets(Vector2 center)
        {
            var centerCellWorld = map.WorldToCell(center);
            cellToWorld = centerCellWorld - centerCellLocal;
            posToWorld = map.CellToWorld(cellToWorld);
            centerPosLocal = center - map.CellToWorld(centerCellWorld) - Map.CellSize2d * 0.5f + centerCellLocal * Map.CellSize2d;
        }

        private void FloodFillVisible()
        {
            swFloodFillVisible.Start();

            Get(centerCellLocal).state = CState.Visible;
            FfvEnqueue(centerCellLocal.y * sizeX + centerCellLocal.x, 0);

            while(PopFfvQueue(out int pos))
            {
                FfvTestNeightbours(pos);
            }

            swFloodFillVisible.Stop();
        }

        private bool PopFfvQueue(out int posXY)
        {
            if (ffvQueue[0].Count > 0)
            {
                ffvQueueCount--;
                posXY = ffvQueue[0].Dequeue();
                return true;
            }
            return PopFfvQueueSlow(out posXY);
        }

        private bool PopFfvQueueSlow(out int posXY)
        {
            if (ffvQueueCount == 0)
            {
                posXY = default;
                return false;
            }

            int f = 1;
            for (; ; f++)
            {
                ffvDistance++;
                if (ffvQueue[f].Count > 0)
                    break;
            }
            int offset = f;
            for (; f < ffvQueue.Length; f++)
            {
                (ffvQueue[f - offset], ffvQueue[f]) = (ffvQueue[f], ffvQueue[f - offset]);
            }

            ffvQueueCount--;
            posXY = ffvQueue[0].Dequeue();
            return true;
        }

        private void FfvTestNeightbours(int posxy)
        {
            int posL = posxy - 1;
            int posR = posxy + 1;
            int posU = posxy - sizeX;
            int posD = posxy + sizeX;

            if (vmap[posU].state == CState.Unknown)
            {
                vmap[posU].state = CState.Visible;
                FfvEnqueue(posU, 3);
            }
            if (vmap[posD].state == CState.Unknown)
            {
                vmap[posD].state = CState.Visible;
                FfvEnqueue(posD, 3);
            }
            if (vmap[posL].state == CState.Unknown)
            {
                vmap[posL].state = CState.Visible;
                FfvEnqueue(posL, 3);
            }
            if (vmap[posR].state == CState.Unknown)
            {
                vmap[posR].state = CState.Visible;
                FfvEnqueue(posR, 3);
            }

            if (vmap[posU].state == CState.Visible)
            {
                if (vmap[posL].state == CState.Visible)
                {
                    var pos = posU - 1;
                    if (vmap[pos].state == CState.Unknown)
                    {
                        vmap[pos].state = CState.Visible;
                        FfvEnqueue(pos, 4);
                    }
                }
                if (vmap[posR].state == CState.Visible)
                {
                    var pos = posU + 1;
                    if (vmap[pos].state == CState.Unknown)
                    {
                        vmap[pos].state = CState.Visible;
                        FfvEnqueue(pos, 4);
                    }
                }
            }

            if (vmap[posD].state == CState.Visible)
            {
                if (vmap[posL].state == CState.Visible)
                {
                    var pos = posD - 1;
                    if (vmap[pos].state == CState.Unknown)
                    {
                        vmap[pos].state = CState.Visible;
                        FfvEnqueue(pos, 4);
                    }
                }
                if (vmap[posR].state == CState.Visible)
                {
                    var pos = posD + 1;
                    if (vmap[pos].state == CState.Unknown)
                    {
                        vmap[pos].state = CState.Visible;
                        FfvEnqueue(pos, 4);
                    }
                }
            }
        }

        private void FfvEnqueue(int posxy, int distDelta)
        {
            if (ffvDistance + distDelta > ffvMaxDistance)
                return;
            ffvQueueCount++;
            ffvQueue[distDelta].Enqueue(posxy);
        }

        #region DrawDebug
        Stack<GameObject> debugMarkers = new Stack<GameObject>();
        Stack<GameObject> debugMarkers2 = new Stack<GameObject>();
        private void DrawDebug()
        {
            while (debugMarkers2.Count > 0) 
            {
                var dm = debugMarkers2.Pop();
                dm.SetActive(false);
                debugMarkers.Push(dm);
            }

            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    var status = Get(new Vector2Int(x, y)).state;
                    if (status != CState.Unknown /*&& status != CState.Visible*/)
                    {
                        GameObject dm;
                        if (debugMarkers.Count > 0)
                        {
                            dm = debugMarkers.Pop();
                            dm.SetActive(true);
                        }
                        else
                        {
                            dm = GameObject.Instantiate(Game.Instance.PrefabsStore.DebugVisibility);
                        }
                        debugMarkers2.Push(dm);

                        dm.transform.position = map.CellToWorld(new Vector2Int(x, y) + cellToWorld);
                        var line = dm.GetComponent<LineRenderer>();

                        var color = status switch
                        {
                            //CState.PartShadow => Color.gray,
                            CState.FullShadow => Color.blue,
                            CState.DSeed => Color.red,
                            CState.Dark => new Color(0.5f, 0.4f, 0.2f),
                            CState.Visible => Color.yellow,
                            _ => Color.magenta
                        };

                        line.startColor = color;
                        line.endColor = color;
                    }
                }
            }
        }

        private short debugDc; 
        private void DrawDebugOne()
        {
            while (debugMarkers2.Count > 0)
            {
                var dm = debugMarkers2.Pop();
                dm.SetActive(false);
                debugMarkers.Push(dm);
            }

            if (debugDc != 0)
            {
                var dc = dcManager.GetDc(debugDc);
                HashSet<Vector2Int> cells = dc.cells.ToHashSet();

                for (int y = 0; y < sizeY; y++)
                {
                    for (int x = 0; x < sizeX; x++)
                    {
                        ref var cell = ref Get(new Vector2Int(x, y));
                        var pos = new Vector2Int(x, y);

                        Color color = Color.black;
                        if (dc.LeftCell == pos || dc.RightCell == pos)
                        {
                            color = Color.yellow;
                        }
                        else if (cells.Contains(pos))
                        {
                            color = Color.red;
                        }
                        else if (cell.darkCaster == debugDc)
                        {
                            color = new Color(0.5f, 0.4f, 0.2f);
                        }

                        if (color != Color.black)
                        {
                            GameObject dm;
                            if (debugMarkers.Count > 0)
                            {
                                dm = debugMarkers.Pop();
                                dm.SetActive(true);
                            }
                            else
                            {
                                dm = GameObject.Instantiate(Game.Instance.PrefabsStore.DebugVisibility);
                            }
                            debugMarkers2.Push(dm);

                            dm.transform.position = map.CellToWorld(new Vector2Int(x, y) + cellToWorld);
                            var line = dm.GetComponent<LineRenderer>();

                            line.startColor = color;
                            line.endColor = color;
                        }
                    }
                }
            }

            debugDc = 0;
        }
        #endregion


        private void DoCircle(int cc, Action<Vector2Int> action, Stopwatch sw, Vector2Int limit)
        {
            bool drawX = cc <= limit.x;
            bool drawY = cc <= limit.y;
            int ccx = drawX ? cc : limit.x + 1;
            int ccy = drawY ? cc : limit.y + 1;


            sw.Start();
            if (drawX)
            {
                action(centerCellLocal + Vector2Int.left * cc);
                action(centerCellLocal + Vector2Int.right * cc);
            }
            if (drawY)
            {
                action(centerCellLocal + Vector2Int.up * cc);
                action(centerCellLocal + Vector2Int.down * cc);
            }

            if (drawX)
            {
                for (int f = 1; f < ccy; f++)
                {
                    action(centerCellLocal + Vector2Int.left * cc + Vector2Int.up * f);
                    action(centerCellLocal + Vector2Int.right * cc + Vector2Int.up * f);
                    action(centerCellLocal + Vector2Int.left * cc + Vector2Int.down * f);
                    action(centerCellLocal + Vector2Int.right * cc + Vector2Int.down * f);
                }
            }

            if (drawY)
            {
                for (int f = 1; f < ccx; f++)
                {
                    action(centerCellLocal + Vector2Int.up * cc + Vector2Int.left * f);
                    action(centerCellLocal + Vector2Int.down * cc + Vector2Int.left * f);
                    action(centerCellLocal + Vector2Int.up * cc + Vector2Int.right * f);
                    action(centerCellLocal + Vector2Int.down * cc + Vector2Int.right * f);
                }
            }

            if (drawX && drawY)
            {
                action(centerCellLocal + Vector2Int.left * cc + Vector2Int.up * cc);
                action(centerCellLocal + Vector2Int.right * cc + Vector2Int.up * cc);
                action(centerCellLocal + Vector2Int.left * cc + Vector2Int.down * cc);
                action(centerCellLocal + Vector2Int.right * cc + Vector2Int.down * cc);
            }
            sw.Stop();
        }

        internal void Test8(Vector2Int center, NeighbourTest action)
        {
            Vector2Int pos;
            pos = new Vector2Int(center.x - 1, center.y - 1);
            action(pos, ref Get(pos));
            pos = new Vector2Int(center.x, center.y - 1);
            action(pos, ref Get(pos));
            pos = new Vector2Int(center.x + 1, center.y - 1);
            action(pos, ref Get(pos));
            pos = new Vector2Int(center.x - 1, center.y);
            action(pos, ref Get(pos));
            pos = new Vector2Int(center.x + 1, center.y);
            action(pos, ref Get(pos));
            pos = new Vector2Int(center.x - 1, center.y + 1);
            action(pos, ref Get(pos));
            pos = new Vector2Int(center.x, center.y + 1);
            action(pos, ref Get(pos));
            pos = new Vector2Int(center.x + 1, center.y + 1);
            action(pos, ref Get(pos));
        }

        internal void Test4(Vector2Int center, NeighbourTest action)
        {
            Vector2Int pos;
            pos = new Vector2Int(center.x, center.y - 1);
            action(pos, ref Get(pos));
            pos = new Vector2Int(center.x - 1, center.y);
            action(pos, ref Get(pos));
            pos = new Vector2Int(center.x + 1, center.y);
            action(pos, ref Get(pos));
            pos = new Vector2Int(center.x, center.y + 1);
            action(pos, ref Get(pos));
        }


        private void CastShadows()
        {
            swCastShadows.Start();

            for (int y = 1; y < HalfYSize * 2; y++)
            {
                int yMult = y * sizeX;
                for (int x = 1; x < HalfXSize * 2; x++)
                {
                    ref var cell = ref vmap[yMult + x];
                    var pos = new Vector2Int(x, y);

                    if (map.GetCellBlocking(pos + cellToWorld).IsDoubleFull())
                    {
                        cell.state = CState.FullShadow;
                        cell.wallType |= WallType.Side | WallType.FloorSet;

                        MarkPartShadowsInNearCells(pos);
                    }
                }
            }

            swCastShadows.Stop();
        }

        private void MarkPartShadowsInNearCells(Vector2Int pos)
        {
            castShadowCounter++;
            int dx = Math.Sign(pos.x - HalfXSize);
            int dy = Math.Sign(pos.y - HalfYSize) * sizeX;
            int p = pos.y * sizeX + pos.x;

            seedsMap[p + dx] += 1;
            seedsMap[p + dy] += 1;
            seedsMap[p + dx + dy] += 1;
        }

        public void MarkPartShadowsInNearCells(Vector2Int pos, int delta)
        {
            int p = (pos.y - 1) * sizeX + pos.x - 1;
            byte d2 = (byte)delta;
            seedsMap[p] += d2;
            p++;
            seedsMap[p] += d2;
            p++;
            seedsMap[p] += d2;

            p += sizeX;
            seedsMap[p] += d2;
            p += sizeX;
            seedsMap[p] += d2;

            p--;
            seedsMap[p] += d2;
            p--;
            seedsMap[p] += d2;

            p -= sizeX;
            seedsMap[p] += d2;
        }


        private void CreateDarkSeeds(Vector2Int pos)
        {
            if (seedsMap[pos.y * sizeX + pos.x] == 0 || Get(pos).state is CState.Visible or CState.Dark)
                return;

            dSeedTestCounter++;
            int score = 0;
            var toCenter = new Vector2Int(Math.Sign(HalfXSize - pos.x), Math.Sign(HalfYSize - pos.y));

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    score += TestShadow(pos + new Vector2Int(x, y), pos, toCenter);
                    if (score > 3)
                        goto breakloop;
                }
            }
            breakloop:

            if (score <= 3)
                dcManager.AddDarkCandidate(pos);
        }


        private int TestShadow(Vector2Int pos, Vector2Int dcPos, Vector2Int toCenter)
        {
            if (IsPosBehind(pos, dcPos, toCenter))
                return 1;
            if (Get(pos).state == CState.Unknown)
                ResolvePartShadow(pos);
            return Get(pos).state switch
            {
                CState.Visible => 1,
                >= CState.FullShadow => 0,
                _ => throw new InvalidOperationException("neocekavany case")
            };
        }

        private void ResolvePartShadow(Vector2Int pos)
        { 
            resolveShadowCounter++;
            Get(pos).state = shadowWorker.ResolvePartShadow(pos) ? CState.FullShadow : CState.Visible;
        }

        private void DrawDarks(int cc)
        {
            swDrawDarks.Start();
            int ccx = cc > HalfXSize ? HalfXSize : cc;
            int ccy = cc > HalfYSize ? HalfYSize : cc;

            var ld = centerCellLocal + Vector2Int.left * ccx + Vector2Int.down * ccy;
            var rd = centerCellLocal + Vector2Int.right * ccx + Vector2Int.down * ccy;
            var lu = centerCellLocal + Vector2Int.left * ccx + Vector2Int.up * ccy;
            var ru = centerCellLocal + Vector2Int.right * ccx + Vector2Int.up * ccy;

            if (cc > HalfXSize)
            {
                DrawDarks(lu, Vector2Int.right, ccx * 2 + 1);
                DrawDarks(rd, Vector2Int.left, ccx * 2 + 1);
            }
            else if (cc > HalfYSize)
            {
                DrawDarks(ru, Vector2Int.down, ccy * 2 + 1);
                DrawDarks(ld, Vector2Int.up, ccy * 2 + 1);
            }
            else
            {
                DrawDarks(lu, Vector2Int.right, ccx * 2);
                DrawDarks(ru, Vector2Int.down, ccy * 2);
                DrawDarks(rd, Vector2Int.left, ccx * 2);
                DrawDarks(ld, Vector2Int.up, ccy * 2);
            }
            swDrawDarks.Stop();
        }

        private void DrawDarks(Vector2Int pos, Vector2Int dir, int count)
        {
            Vector2 center = CellCenter(pos);
            Vector2 centerDir = TurnLeft(dir);
            Vector2 toCenter = center - centerPosLocal;
            Vector2 posDelta = (Vector2)dir * 0.5f;

            short draw = darkBorders.StartDrawing(toCenter, out var ptr, out var nextDir, out var point);
            if (Vector2.Dot(nextDir, centerDir) <= 0)
                ptr = DarkBorders.BorderPtr.Null;
            Vector2 normal = TurnLeft(nextDir);
            Vector2 pToC = center - point;
            while (count > 0)
            {
                if (ptr.AbsRelArrDist <= 1 && Vector2.Dot(pToC, normal) < 0)
                {
                    draw = darkBorders.ContinueDrawing(ref ptr, out nextDir, out point);
                    if (Vector2.Dot(nextDir, centerDir) <= 0)
                        ptr = DarkBorders.BorderPtr.Null;
                    center = CellCenter(pos);
                    normal = TurnLeft(nextDir);
                    pToC = center - point;
                }

                count--;

                if (draw != -1)
                {
                    ref var cell = ref Get(pos);
                    cell.state = CState.Dark;
                    cell.darkCaster = draw;
                }

                pToC += posDelta;
                pos += dir;
            }
        }


        private void BuildMeshes()
        {
            try
            {
                swBuildMesh.Start();
                dcManager.BuildMeshes(centerPosLocal);
                swBuildMesh.Stop();
            }
            catch (CyclusException ex)
            {
                UnityEngine.Debug.LogError($"{ex.Message} {ex.DcId}");
                debugDc = ex.DcId;
            }
        }


        private bool IsPosBehind(Vector2Int pos, Vector2Int dcPos, Vector2Int toCenter)
        {
            var dx = pos - dcPos;
            return dx.x * toCenter.x + dx.y * toCenter.y < 0;
        }

        internal ref Cell Get(Vector2Int coords) => ref vmap[coords.y * sizeX + coords.x];
        internal static bool CellValid(Vector2Int coords) => coords.x >= 0 && coords.y >= 0 && coords.x < sizeX && coords.y < sizeY;
        internal static Vector2 CellPivot(Vector2Int coords) => (Vector2)coords * 0.5f;
        internal static Vector2 CellCenter(Vector2Int coords) => (Vector2)coords * 0.5f + new Vector2(0.25f, 0.25f);
        // anti clock wise:
        internal static bool IsBetterOrder(Vector2 first, Vector2 second) => first.x * second.y - first.y * second.x > 0;
        internal static float CrossProduct(Vector2 first, Vector2 second) => first.x * second.y - first.y * second.x;
        internal static Vector2 TurnLeft(Vector2 vec) => new Vector2(-vec.y, vec.x);
        internal static Vector2Int TurnLeft(Vector2Int vec) => new Vector2Int(-vec.y, vec.x);

        private void Reset()
        {
            dcManager.FreeDarkCasters();
            vmap.AsSpan().Clear();
            seedsMap.AsSpan().Clear();
            darkBorders.Clear();
            ffvDistance = 0;

            swFloodFillVisible.Reset();
            swCastShadows.Reset();
            swCreateDcs.Reset();
            swCreateSeeds.Reset();
            swDrawDarks.Reset();
            swBuildMesh.Reset();
            castShadowCounter = 0;
            dSeedTestCounter = 0;
            resolveShadowCounter = 0;
        }

        internal void ReportDiagnostics(double[] visibiltyTimes, int[] visibilityCounters)
        {
            visibiltyTimes[0] = swCastShadows.Elapsed.TotalMilliseconds;
            visibiltyTimes[1] = swFloodFillVisible.Elapsed.TotalMilliseconds;
            visibiltyTimes[2] = swCreateSeeds.Elapsed.TotalMilliseconds;
            visibiltyTimes[3] = swDrawDarks.Elapsed.TotalMilliseconds;
            visibiltyTimes[4] = swCreateDcs.Elapsed.TotalMilliseconds;
            visibiltyTimes[5] = swBuildMesh.Elapsed.TotalMilliseconds;

            visibilityCounters[0] = castShadowCounter;
            visibilityCounters[1] = dSeedTestCounter;
            visibilityCounters[2] = resolveShadowCounter;
            visibilityCounters[3] = dcManager.dcAbandonCounter;
            visibilityCounters[4] = dcManager.dcCreateCounter;
            visibilityCounters[5] = dcManager.dcBorderAddCounter;

        }

        internal void ReMarkCells(List<Vector2Int> cells, short markId)
        {
            foreach (var cell in cells)
                Get(cell).darkCaster = markId;
        }
    }
}
