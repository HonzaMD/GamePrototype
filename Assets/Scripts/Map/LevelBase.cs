using Assets.Scripts.Core;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;

namespace Assets.Scripts.Map
{
    abstract class LevelBase
    {
        protected abstract string[] Data { get; }
        private Vector2Int levelOffset;
        private Vector2 levelRootPos;

        public int SizeX => Data[0].Length;
        public int SizeY => Data.Length;

        public IEnumerable<(ILevelPlaceabe, Vector3)> Placeables(PrefabsStore prefabsStore, LvlBuildMode buildMode, Transform levelRoot, Vector2Int levelOffset)
        {
            this.levelOffset = levelOffset;
            levelRootPos = levelRoot.position.XY();
            Assert.IsTrue(Data.GroupBy(s => s.Length).Count() == 1);

            var delayed = new List<(ILevelPlaceabe, Vector3)>();

            int y = Data.Length - 1;
            foreach (string str in Data)
            {
                int x = 0;
                foreach (char ch in str)
                {
                    Vector2 pos = ToWorld(x, y);

                    var p = PlaceableFromChar(ch, prefabsStore, x, y, buildMode);
                    if (p != null)
                    {
                        if (p.SecondPhase)
                            delayed.Add((p, pos));
                        else
                            yield return (p, pos);
                    }

                    x++;
                }
                y--;
            }

            foreach (var d in delayed)
                yield return d;
        }

        private Vector2 ToWorld(int x, int y)
        {
            var pos = new Vector2(x + levelOffset.x, y + levelOffset.y);
            pos.Scale(Map.CellSize2d);
            return pos + levelRootPos;
        }

        private ILevelPlaceabe PlaceableFromChar(char ch, PrefabsStore prefabsStore, int x, int y, LvlBuildMode buildMode)
        {
            if (buildMode != LvlBuildMode.All && ch != 'H')
                return null;

            switch (ch)
            {
                case 'H': return prefabsStore.Block;
                case ' ': return null;
                case 'L':
                case 'R': return SearchLadder(ch, x, y, prefabsStore);
                case 'O':
                case 'P': return SearchRope(ch, x, y);
                case 'm': return prefabsStore.SmallMonster;
                case 's': return prefabsStore.Stone;
                case '*': return prefabsStore.PointLight;
                case 'A': return prefabsStore.Character;
                default: throw new InvalidOperationException("Nezname pismeno");
            }
        }

        private ILevelPlaceabe SearchLadder(char ch, int x, int y, PrefabsStore prefabsStore)
        {
            if (!SearchLongItem(ch, ch == 'L', x, y, out var start, out var end))
                return null;

            return new LadderPlacer(start, end, Map.CellSize.z);
        }

        private ILevelPlaceabe SearchRope(char ch, int x, int y)
        {
            if (!SearchLongItem(ch, ch == 'O', x, y, out var start, out var end))
                return null;

            return new RopePlacer(start, end, Map.CellSize.z, start.x != end.x);
        }

        private bool SearchLongItem(char ch, bool lowCorner, int x, int y, out Vector2 start, out Vector2 end)
        {
            int xFrom = int.MaxValue;
            int yFrom = int.MaxValue;
            if (GetNeighnbour(ch, ref x, ref y, ref xFrom, ref yFrom) == 1)
            {
                start = lowCorner ? ToWorld(xFrom, yFrom) : ToWorld(xFrom + 1, yFrom + 1);

                while (true)
                {
                    int count = GetNeighnbour(ch, ref x, ref y, ref xFrom, ref yFrom);
                    if (count == 0) 
                        break;
                    if (count > 1)
                        throw new InvalidOperationException("Ladder pismeno pismena netvori jednu linii!");
                }

                end = lowCorner ? ToWorld(x, y) : ToWorld(x + 1, y + 1);
                if (start == end)
                    throw new InvalidOperationException("Ladder pismeno je jen jedno!");

                if (start.y > end.y || (start.y == end.y && start.x < end.x))
                    return true;
            }

            start = default;
            end = default;
            return false;
        }

        private int GetNeighnbour(char ch, ref int x, ref int y, ref int xFrom, ref int yFrom)
        {
            int counter = 0;
            int x2 = 0;
            int y2 = 0;
            GetNeigbour1(ch, x - 1, y - 1, xFrom, yFrom, ref counter, ref x2, ref y2);
            GetNeigbour1(ch, x,     y - 1, xFrom, yFrom, ref counter, ref x2, ref y2);
            GetNeigbour1(ch, x + 1, y - 1, xFrom, yFrom, ref counter, ref x2, ref y2);
            GetNeigbour1(ch, x - 1, y,     xFrom, yFrom, ref counter, ref x2, ref y2);
            GetNeigbour1(ch, x + 1, y,     xFrom, yFrom, ref counter, ref x2, ref y2);
            GetNeigbour1(ch, x - 1, y + 1, xFrom, yFrom, ref counter, ref x2, ref y2);
            GetNeigbour1(ch, x,     y + 1, xFrom, yFrom, ref counter, ref x2, ref y2);
            GetNeigbour1(ch, x + 1, y + 1, xFrom, yFrom, ref counter, ref x2, ref y2);
            if (counter == 1)
            {
                xFrom = x; yFrom = y;
                x = x2; y = y2;
            }
            return counter;
        }

        private void GetNeigbour1(char ch, int x, int y, int xFrom, int yFrom, ref int counter, ref int x2, ref int y2)
        {
            if (GetChar(x, y) == ch && !(xFrom == x && yFrom == y))
            {
                counter++;
                x2 = x;
                y2 = y;
            }
        }

        private char GetChar(int x, int y) => (x < 0 || y < 0 || x >= Data[0].Length || y >= Data.Length) ? ' ' : Data[Data.Length - y - 1][x];

        private class LadderPlacer : ILevelPlaceabe
        {
            private readonly Vector2 start;
            private readonly Vector2 end;
            private readonly float z;

            public LadderPlacer(Vector2 start, Vector2 end, float z)
            {
                this.start = start;
                this.end = end;
                this.z = z;
            }

            void ILevelPlaceabe.Instantiate(Map map, Transform parent, Vector3 pos)
            {
                PlankSegment.AddToMap(map, Game.Instance.PrefabsStore.LadderSegment, parent, start.AddZ(z), end.AddZ(z));
            }
            bool ILevelPlaceabe.SecondPhase => true;
        }

        private class RopePlacer : ILevelPlaceabe
        {
            private readonly Vector2 start;
            private readonly Vector2 end;
            private readonly float z;
            private readonly bool fixEnd;

            public RopePlacer(Vector2 start, Vector2 end, float z, bool fixEnd)
            {
                this.start = start;
                this.end = end;
                this.z = z;
                this.fixEnd = fixEnd;
            }

            void ILevelPlaceabe.Instantiate(Map map, Transform parent, Vector3 pos)
            {
                RopeSegment.AddToMap(map, parent, start.AddZ(z), end.AddZ(z), fixEnd);
            }
            bool ILevelPlaceabe.SecondPhase => true;
        }
    }
}
