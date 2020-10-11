using Assets.Scripts.Core;
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


        public IEnumerable<(Placeable, Vector3)> Placeables(PrefabsStore prefabsStore)
        {
            int y = Data.Length - 1;
            foreach (string str in Data)
            {
                int x = 0;
                foreach (char ch in str)
                {
                    var pos = new Vector2(x, y);
                    pos.Scale(Map.CellSize2d);
                    pos += map.mapOffset;

                    if (ch != ' ')
                        yield return (PlaceableFromChar(ch, prefabsStore), pos);

                    x++;
                }
                y--;
            }
        }

        private Placeable PlaceableFromChar(char ch, PrefabsStore prefabsStore)
        {
            switch (ch)
            {
                case 'H': return prefabsStore.Block;
                default: throw new InvalidOperationException("Nezname pismeno");
            }
        }
    }
}
