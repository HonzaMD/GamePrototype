using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Map
{
    internal class ScenesMap
    {
        private List<Column> columns = new();
        private Column searcher = new();

        private class Column : IComparable<Column>
        {
            public int x;
            public List<(int y, Scene scene)> cells = new();

            public int CompareTo(Column other) => x.CompareTo(other.x);
        }

        public void Add(Scene scene, int x , int y)
        {
            searcher.x = x;
            int i = columns.BinarySearch(searcher);
            if (i < 0)
            {
                i = ~i;
                columns.Insert(i, new Column() { x = x });
            }

            columns[i].cells.Add((y, scene));
        }

        public void Remove(Scene scene, int x, int y)
        {
            searcher.x = x;
            int i = columns.BinarySearch(searcher);
            if (i >= 0)
            {
                var cells = columns[i].cells;
                for (int j = 0; j < cells.Count; j++)
                {
                    if (cells[j].scene == scene)
                    {
                        cells.RemoveAt(j);
                        break;
                    }
                }
            }
        }

        public Scene Find(int x, int y)
        {
            searcher.x = x;
            int i = columns.BinarySearch(searcher);
            if (i < 0)
            {
                i = ~i - 1;
            }

            if (i < 0 || i >= columns.Count)
                return default;

            var cells = columns[i].cells;

            Scene ret = default;
            int rety = int.MinValue;

            foreach (var p in cells)
            {
                if (p.y <= y && p.y > rety)
                {
                    ret = p.scene;
                    rety = p.y;
                }
            }

            return ret;
        }
    }
}
