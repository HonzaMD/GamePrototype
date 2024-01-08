using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Map
{
    public partial class Visibility
    {
        public const int HalfXSize = 32;
        public const int HalfYSize = 26;

        private const int sizeX = HalfXSize * 2 + 1;
        private const int sizeY = HalfYSize * 2 + 1;

        private const float centerRadius = 0.7f;

        private readonly Map map;
        private Cell[] vmap = new Cell[sizeY * sizeX];
        private Vector2 centerPosLocal; // pozice od stedu mapy
        private Vector2Int offset;

        private Action<Vector2Int> castShadowAction;
        private Action<Vector2Int> createDarkersAction;
        private readonly ShadowWorker shadowWorker;

        private enum CState : byte
        {
            Unknown,
            Visible,    // neni full shadow (muzu prejit z PartShadow po testu)
            PartShadow, // kandidat na fullShadow (pak musim udelat detailni test
            FullShadow, // potreba pro detekci Darku. Vsechny stavy >= FullShadow odpovidaji FS
            Dark,       // bunka ve stinu darkCasteru
            DarkCandidate, // kandidat na darkCaster
        }

        [Flags]
        private enum WallType : byte
        {
            None = 0,
            Floor = 1,
            Side = 2,
            FloorSet = 1 | 4,
            LeftShadow = 8,
            RightShadow = 16,
            LeftShadowSet = 8 | 32,
        }

        private struct Cell 
        { 
            public CState state;
            public WallType wallType;

            public readonly bool IsFloor(int shift) => state >= CState.FullShadow || (((int)WallType.Floor << shift) & (int)wallType) != 0;
            public readonly bool IsSide(int shift) => state >= CState.FullShadow || (((int)WallType.Side << shift) & (int)wallType) != 0;
        }


        public Visibility(Map map)
        {
            this.map = map;
            shadowWorker = new(this);
            castShadowAction = CastShadows;
            createDarkersAction = CreateDarkers;
        }

        public void Compute(Vector2 center)
        {
            var centerCell = map.WorldToCell(center);
            var centerLocal = new Vector2Int(HalfXSize, HalfYSize);

            offset = centerCell - centerLocal;
            centerPosLocal = center - map.CellToWorld(centerCell) - Map.CellSize2d * 0.5f + centerLocal * Map.CellSize2d;
            Reset();

            Get(centerLocal).state = CState.Visible;
            int maxCircles = Math.Max(HalfYSize, HalfXSize);

            for (int cc = 1; cc <= maxCircles; cc++)
            {
                DoCircle(cc, castShadowAction);
                if (cc > 1)
                    DoCircle(cc - 1, createDarkersAction);
            }

            DrawDebug();
        }

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
                    if (status != CState.Visible)
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

                        dm.transform.position = map.CellToWorld(new Vector2Int(x, y) + offset);
                        var line = dm.GetComponent<LineRenderer>();

                        var color = status switch
                        {
                            CState.PartShadow => Color.gray,
                            CState.FullShadow => Color.blue,
                            CState.DarkCandidate => Color.red,
                            CState.Dark => Color.black,
                            _ => Color.magenta
                        };

                        line.startColor = color;
                        line.endColor = color;
                    }
                }
            }
        }

        private void DoCircle(int cc, Action<Vector2Int> action)
        {
            var centerLocal = new Vector2Int(HalfXSize, HalfYSize);

            action(centerLocal + Vector2Int.left * cc);
            action(centerLocal + Vector2Int.right * cc);
            action(centerLocal + Vector2Int.up * cc);
            action(centerLocal + Vector2Int.down * cc);

            for (int f = 1; f < cc; f++)
            {
                action(centerLocal + Vector2Int.left * cc + Vector2Int.up * f);
                action(centerLocal + Vector2Int.right * cc + Vector2Int.up * f);
                action(centerLocal + Vector2Int.up * cc + Vector2Int.left * f);
                action(centerLocal + Vector2Int.down * cc + Vector2Int.left * f);
                action(centerLocal + Vector2Int.left * cc + Vector2Int.down * f);
                action(centerLocal + Vector2Int.right * cc + Vector2Int.down * f);
                action(centerLocal + Vector2Int.up * cc + Vector2Int.right * f);
                action(centerLocal + Vector2Int.down * cc + Vector2Int.right * f);
            }

            action(centerLocal + Vector2Int.left * cc + Vector2Int.up * cc);
            action(centerLocal + Vector2Int.right * cc + Vector2Int.up * cc);
            action(centerLocal + Vector2Int.left * cc + Vector2Int.down * cc);
            action(centerLocal + Vector2Int.right * cc + Vector2Int.down * cc);
        }

        private void CastShadows(Vector2Int pos)
        {
            if (!CellValid(pos))
                return;

            ref var cell = ref Get(pos);

            if (cell.state == CState.Dark)
            {
                return;
            }
            else if (map.GetCellBlocking(pos + offset).IsDoubleFull())
            {
                cell.state = CState.FullShadow;
                cell.wallType = WallType.Side | WallType.FloorSet;

                MarkPartShadowsInNearCells(pos);

            }
            else if (cell.state == CState.Unknown)
            {
                cell.state = CState.Visible;
            }
        }

        private void MarkPartShadowsInNearCells(Vector2Int pos)
        {
            int dx = Math.Sign(pos.x - HalfXSize);
            int dy = Math.Sign(pos.y - HalfYSize);

            int maxX = dx == 0 ? 1 : 3;
            int maxY = dy == 0 ? 1 : 3;

            for (int x = 0; x < maxX; x++)
            {
                for (int y = 0; y < maxY; y++)
                {
                    if (x != 0 || y != 0)
                    {
                        var pos2 = new Vector2Int(x * dx, y * dy) + pos;
                        if (CellValid(pos2))
                        {
                            ref var cell2 = ref Get(pos2);
                            if (cell2.state == CState.Unknown)
                                cell2.state = CState.PartShadow;
                        }
                    }
                }
            }
        }

        private void CreateDarkers(Vector2Int pos)
        {
            if (!CellValid(pos))
                return;

            if (Get(pos).state is CState.Visible or CState.Dark)
                return;

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
                Get(pos).state = CState.DarkCandidate;
        }

        private int TestShadow(Vector2Int pos, Vector2Int dcPos, Vector2Int toCenter)
        {
            if (!CellValid(pos))
                return 1;
            if (IsPosBehind(pos, dcPos, toCenter))
                return 1;
            if (Get(pos).state == CState.PartShadow)
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
            Get(pos).state = shadowWorker.ResolvePartShadow(pos) ? CState.FullShadow : CState.Visible;
        }



        //private static readonly (int cx, int cy, WallType wallType, bool noTest, float sx, float sy, float ex, float ey)[] startTests = new[]
        //{
        //    (0, 0, WallType.Floor, true, 0f, 0.5f, 0.5f, 0.5f),
        //    (0, 1, WallType.Floor, true, 0f, 1f, 0.5f, 1f),
        //    (0, 2, WallType.Floor, true, 0f, 1.5f, 0.5f, 1.5f),
        //    (1, 2, WallType.Floor, false, 0.5f, 1.5f, 1f, 1.5f),

        //    (0, 2, WallType.Side, false, 0.5f, 1.5f, 0.5f, 1f),
        //    (1, 1, WallType.Floor, false, 0.5f, 1f, 1f, 1f),
        //    (0, 1, WallType.Side, false, 0.5f, 1f, 0.5f, 0.5f),
        //    (1, 0, WallType.Floor, false, 0.5f, 0.5f, 1f, 0.5f),
        //    (0, 0, WallType.Side, false, 0.5f, 0.5f, 0.5f, 0f),

        //    (2, 2, WallType.Floor, false, 1f, 1.5f, 1.5f, 1.5f),
        //    (1, 2, WallType.Side, false, 1f, 1.5f, 1f, 1f),
        //    (2, 1, WallType.Floor, false, 1f, 1f, 1.5f, 1f),
        //    (1, 1, WallType.Side, false, 1f, 1f, 1f, 0.5f),

        //    (2, 2, WallType.Side, false, 1.5f, 1.5f, 1.5f, 1f),
        //};


        private bool IsPosBehind(Vector2Int pos, Vector2Int dcPos, Vector2Int toCenter)
        {
            var dx = pos - dcPos;
            return dx.x * toCenter.x + dx.y * toCenter.y < 0;
        }

        ref Cell Get(Vector2Int coords) => ref vmap[coords.y * sizeX + coords.x];
        bool CellValid(Vector2Int coords) => coords.x >= 0 && coords.y >= 0 && coords.x < sizeX && coords.y < sizeY;

        private void Reset()
        {
            vmap.AsSpan().Clear();
        }
    }
}
