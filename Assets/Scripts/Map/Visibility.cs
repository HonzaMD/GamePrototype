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
        private const float centerRadiusSq = centerRadius * centerRadius;
        private const float centerRadiusMarginSq = centerRadius * centerRadius * 1.8f * 1.8f;

        private readonly Map map;
        private Cell[] vmap = new Cell[sizeY * sizeX];
        private Vector2 centerPosLocal; // pozice od stedu mapy
        private Vector2Int offset;

        private delegate void NeighbourTest(Vector2Int pos, ref Cell cell);
        private readonly Action<Vector2Int> castShadowAction;
        private readonly Action<Vector2Int> createDarkersAction;
        private readonly NeighbourTest findActiveCasterAction;
        private readonly NeighbourTest joinOtherCasterAction;
        private readonly NeighbourTest findTouchingShadowAction;
        private readonly NeighbourTest recolorDCAction;

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
            public short darkCaster;

            public readonly bool IsFloor(int shift) => state >= CState.FullShadow || (((int)WallType.Floor << shift) & (int)wallType) != 0;
            public readonly bool IsSide(int shift) => state >= CState.FullShadow || (((int)WallType.Side << shift) & (int)wallType) != 0;
        }


        public Visibility(Map map)
        {
            this.map = map;
            shadowWorker = new(this);
            castShadowAction = CastShadows;
            createDarkersAction = CreateDarkers;
            findActiveCasterAction = FindActiveCaster;
            joinOtherCasterAction = JoinOtherCaster;
            findTouchingShadowAction = FindTouchingShadow;
            recolorDCAction = RecolorDC;
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

                TryCastDark();
            }
            TryCastDark();

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
                            CState.Dark => new Color(0.5f, 0.4f, 0.2f),
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

        private void Test8(Vector2Int center, NeighbourTest action)
        {
            if (center.x > 0 && center.y > 0 && center.x < sizeX - 1 && center.y < sizeY - 1)
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
            else
            {
                Test8Slow(center, action);
            }
        }

        private void Test8Slow(Vector2Int center, NeighbourTest action)
        {
            Vector2Int pos;
            pos = new Vector2Int(center.x - 1, center.y - 1);
            if (CellValid(pos))
                action(pos, ref Get(pos));
            pos = new Vector2Int(center.x, center.y - 1);
            if (CellValid(pos))
                action(pos, ref Get(pos));
            pos = new Vector2Int(center.x + 1, center.y - 1);
            if (CellValid(pos))
                action(pos, ref Get(pos));
            pos = new Vector2Int(center.x - 1, center.y);
            if (CellValid(pos))
                action(pos, ref Get(pos));
            pos = new Vector2Int(center.x + 1, center.y);
            if (CellValid(pos))
                action(pos, ref Get(pos));
            pos = new Vector2Int(center.x - 1, center.y + 1);
            if (CellValid(pos))
                action(pos, ref Get(pos));
            pos = new Vector2Int(center.x, center.y + 1);
            if (CellValid(pos))
                action(pos, ref Get(pos));
            pos = new Vector2Int(center.x + 1, center.y + 1);
            if (CellValid(pos))
                action(pos, ref Get(pos));
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
                AddDarkCandidate(pos);
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



        private bool IsPosBehind(Vector2Int pos, Vector2Int dcPos, Vector2Int toCenter)
        {
            var dx = pos - dcPos;
            return dx.x * toCenter.x + dx.y * toCenter.y < 0;
        }

        private ref Cell Get(Vector2Int coords) => ref vmap[coords.y * sizeX + coords.x];
        private static bool CellValid(Vector2Int coords) => coords.x >= 0 && coords.y >= 0 && coords.x < sizeX && coords.y < sizeY;
        private static Vector2 CellPivot(Vector2Int coords) => (Vector2)coords * 0.5f;

        private void Reset()
        {
            FreeDarkCasters();
            vmap.AsSpan().Clear();
        }
    }
}
