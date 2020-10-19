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
        private Map map;

        public Map CreateMap(Ksids ksids)
        {
            Assert.IsTrue(Data.GroupBy(s => s.Length).Count() == 1);
            int sizex = Data[0].Length;
            int sizey = Data.Length;

            map = new Map(-sizex / 2, -sizey / 2, sizex, sizey, ksids);
            return map;
        }


        public IEnumerable<(ILevelPlaceabe, Vector3)> Placeables(PrefabsStore prefabsStore)
        {
            int y = Data.Length - 1;
            foreach (string str in Data)
            {
                int x = 0;
                foreach (char ch in str)
                {
                    Vector2 pos = ToWorld(x, y);

                    var p = PlaceableFromChar(ch, prefabsStore, x, y);
                    if (p != null)
                        yield return (p, pos);

                    x++;
                }
                y--;
            }
        }

        private Vector2 ToWorld(int x, int y)
        {
            var pos = new Vector2(x, y);
            pos.Scale(Map.CellSize2d);
            pos += map.mapOffset;
            return pos;
        }

        private ILevelPlaceabe PlaceableFromChar(char ch, PrefabsStore prefabsStore, int x, int y)
        {
            switch (ch)
            {
                case 'H': return prefabsStore.Block;
                case ' ': return null;
                case 'L':
                case 'R': return SearchLadder(ch, x, y, prefabsStore);
                case 'O':
                case 'P': return SearchRope(ch, x, y, prefabsStore);
                case 'm': return prefabsStore.SmallMonster;
                default: throw new InvalidOperationException("Nezname pismeno");
            }
        }

        private ILevelPlaceabe SearchLadder(char ch, int x, int y, PrefabsStore prefabsStore)
        {
            if (!SearchLongItem(ch, ch == 'L', x, y, out var start, out var end))
                return null;

            return new LadderPlacer(prefabsStore.Ladder, start, end, Map.CellSize.z);
        }

        private ILevelPlaceabe SearchRope(char ch, int x, int y, PrefabsStore prefabsStore)
        {
            if (!SearchLongItem(ch, ch == 'O', x, y, out var start, out var end))
                return null;

            return new RopePlacer(prefabsStore.Rope, end, start, Map.CellSize.z, start.x != end.x);
        }

        private bool SearchLongItem(char ch, bool lowCorner, int x, int y, out Vector2 start, out Vector2 end)
        {
            if (GetChar(x - 1, y) == ch || GetChar(x, y - 1) == ch || GetChar(x - 1, y - 1) == ch)
            {
                start = default;
                end = default;
                return false;
            }
            
            start = lowCorner ? ToWorld(x, y) : ToWorld(x + 1, y + 1);
            while (true)
            {
                if (GetChar(x + 1, y) == ch)
                    x++;
                else if (GetChar(x, y + 1) == ch)
                    y++;
                else if (GetChar(x + 1, y + 1) == ch)
                {
                    x++;
                    y++;
                }
                else
                    break;
            }

            end = lowCorner ? ToWorld(x, y) : ToWorld(x + 1, y + 1);
            if (start == end)
                throw new InvalidOperationException("Ladder pismeno je jen jedno!");
            return true;
        }

        private char GetChar(int x, int y) => (x < 0 || y < 0 || x >= Data[0].Length || y >= Data.Length) ? ' ' : Data[Data.Length - y - 1][x];

        private class LadderPlacer : ILevelPlaceabe
        {
            private readonly Plank plank;
            private readonly Vector2 start;
            private readonly Vector2 end;
            private readonly float z;

            public LadderPlacer(Plank plank, Vector2 start, Vector2 end, float z)
            {
                this.plank = plank;
                this.start = start;
                this.end = end;
                this.z = z;
            }

            void ILevelPlaceabe.Instantiate(Map map, Transform parent, Vector3 pos)
            {
                var p = UnityEngine.Object.Instantiate(plank, parent);
                p.AddToMap(map, start.AddZ(z), end.AddZ(z));
            }
        }

        private class RopePlacer : ILevelPlaceabe
        {
            private readonly Rope rope;
            private readonly Vector2 start;
            private readonly Vector2 end;
            private readonly float z;
            private readonly bool fixEnd;

            public RopePlacer(Rope rope, Vector2 start, Vector2 end, float z, bool fixEnd)
            {
                this.rope = rope;
                this.start = start;
                this.end = end;
                this.z = z;
                this.fixEnd = fixEnd;
            }

            void ILevelPlaceabe.Instantiate(Map map, Transform parent, Vector3 pos)
            {
                var r = UnityEngine.Object.Instantiate(rope, parent);
                r.AddToMap(map, start.AddZ(z), end.AddZ(z), fixEnd);
            }
        }
    }
}
