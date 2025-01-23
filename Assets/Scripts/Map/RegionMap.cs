using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Map
{
    public class RegionMap
    {
        private List<Column> columns = new() { new Column() { x = float.MinValue } };
        private Column searcher = new();

        private class Column : IComparable<Column>
        {
            public  float x;
            public readonly List<(float y, float y2)> cells;

            public Column(float x, List<(float y, float y2)> cells)
            {
                this.x = x;
                this.cells = new(cells);
            }

            public Column()
            {
                cells = new();
            }
            
            public int CompareTo(Column other) => x.CompareTo(other.x);

            internal void InsertCell(float y, float y2)
            {
                for (int i = 0; i < cells.Count; )
                {
                    if (Overlaps(ref y, ref y2, cells[i].y, cells[i].y2))
                        cells.RemoveAt(i);
                    else
                        i++;
                }
                cells.Add((y, y2));
            }

            private static bool Overlaps(ref float my1, ref float my2, float other1, float other2)
            {
                if (my1 < other1)
                {
                    if (my2 >= other1)
                    {
                        my2 = MathF.Max(my2, other2);
                        return true;
                    }
                }
                else if ( my1 <= other2)
                {
                    my1 = other1;
                    my2 = MathF.Max(my2, other2);
                    return true;
                }
                return false;
            }
        }

        public void Add(float x, float y, float x2, float y2)
        {
            int i1 = InsertX(x);
            int i2 = InsertX(x2);

            for (int i = i1; i < i2; ++i)
            {
                columns[i].InsertCell(y, y2);
            }
        }

        private int InsertX(float x)
        {
            searcher.x = x;
            int i = columns.BinarySearch(searcher);
            if (i < 0)
            {
                i = ~i;
                var column = new Column(x, columns[i - 1].cells);
                columns.Insert(i, column);
            }
            return i;
        }


        public bool Find(float x, float y)
        {
            searcher.x = x;
            int i = columns.BinarySearch(searcher);
            if (i < 0)
            {
                i = ~i - 1;
            }


            foreach (var p in columns[i].cells)
            {
                if (p.y <= y && y <= p.y2)
                {
                    return true;
                }
            }

            return false;
        }

        internal bool Find(Transform transform)
        {
            var v = transform.position.XY();
            return Find(v.x, v.y);
        }
    }
}
