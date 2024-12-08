using Assets.Scripts.Core;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Map
{
    public interface ICell
    {
        void Add(Placeable p, Ksids ksids);
        public void Remove(Placeable p, Ksids ksids);
        public Cell.Enumerator GetEnumerator();
    }

    public abstract class MapBase<T>
        where T : ICell
    {
        protected readonly int posx;
        protected readonly int posy;
        protected readonly int sizex;
        protected readonly int sizey;
        protected readonly T[] cells;

        protected readonly Vector2 cellSize2D;
        protected readonly Vector2 cellSize2dInv;
        public readonly Vector2 mapOffset;
        public readonly Vector2Int mapSize;

        protected readonly Ksids ksids;
        protected T emptyCell = default;

        public MapBase(int posx, int posy, int sizex, int sizey, Ksids ksids, Vector2 cellSize2d)
        {
            this.posx = posx;
            this.posy = posy;
            this.sizex = sizex;
            this.sizey = sizey;

            cellSize2D = cellSize2d;
            cellSize2dInv = new Vector2(1f / cellSize2d.x, 1f / cellSize2d.y);

            mapSize = new Vector2Int(sizex, sizey);
            var mo = new Vector2(posx, posy);
            mo.Scale(cellSize2d);
            mapOffset = mo;

            this.ksids = ksids;            
            cells = new T[sizex * sizey];
        }

        public MapSettings Settings => new MapSettings(posx, posy, sizex, sizey);




        internal void GetEverything(List<Placeable> output)
        {
            var tag = GetNextTag();
            for (int f = 0; f < cells.Length; f++)
            {
                foreach (var p in cells[f])
                {
                    if (p.Tag != tag)
                    {
                        p.Tag = tag;
                        output.Add(p);
                    }
                }
            }
        }

        public abstract int GetNextTag();

        internal void ResetTags()
        {
            for (int f = 0; f < cells.Length; f++)
            {
                foreach(var p in cells[f])
                {
                    p.Tag = 0;
                }
            }
        }



        public ref T GetCell(Vector2 pos)
        {
            if (WorldToCell(pos, out var cellPos))
            {
                return ref cells[cellPos];
            }
            else 
            {
                return ref emptyCell;
            }
        }
        public ref T GetCell(Vector2Int pos)
        {
            if (CellToCell(pos, out var cellPos))
            {
                return ref cells[cellPos];
            }
            else
            {
                return ref emptyCell;
            }
        }


        public bool ContainsType(Vector2 pos, Ksid ksid)
        {
            if (WorldToCell(pos, out var cellPos))
            {
                foreach (var p in cells[cellPos])
                {
                    if (ksids.IsParentOrEqual(p.Ksid, ksid))
                        return true;
                }
            }
            return false;
        }
        public bool ContainsType(Vector2Int pos, Ksid ksid)
        {
            if (CellToCell(pos, out var cellPos))
            {
                foreach (var p in cells[cellPos])
                {
                    if (ksids.IsParentOrEqual(p.Ksid, ksid))
                        return true;
                }
            }
            return false;
        }

        public bool ContainsType(Vector2 pos, Vector2 size, Ksid ksid)
        {
            int tag = GetNextTag();

            #region Coords Prep CopyPaste
            pos -= mapOffset;
            pos.Scale(cellSize2dInv);
            var pos2 = new Vector2(size.x * cellSize2dInv.x + pos.x, size.y * cellSize2dInv.y + pos.y);

            var posFl = Vector2Int.FloorToInt(pos);
            var pos2Cl = Vector2Int.CeilToInt(pos2);
            if (posFl.x == pos2Cl.x)
                pos2Cl.x += 1;
            if (posFl.y == pos2Cl.y)
                pos2Cl.y += 1;
            posFl = Vector2Int.Max(Vector2Int.zero, posFl);
            pos2Cl = Vector2Int.Min(mapSize, pos2Cl);

            int cellPosY = posFl.y * sizex;
            #endregion
            for (int y = posFl.y; y < pos2Cl.y; y++, cellPosY += sizex)
            {
                for (int x = posFl.x; x < pos2Cl.x; x++)
                {
                    foreach (var p in cells[cellPosY + x])
                    {
                        if (p.Tag != tag)
                        {
                            p.Tag = tag;
                            if (ksids.IsParentOrEqual(p.Ksid, ksid))
                                return true;
                        }
                    }
                }
            }

            return false;
        }



        public void Get(List<Placeable> output, Vector2 pos, Vector2 size, Ksid ksid, int tag = 0)
        {
            if (tag == 0)
                tag = GetNextTag();

            #region Coords Prep CopyPaste
            pos -= mapOffset;
            pos.Scale(cellSize2dInv);
            var pos2 = new Vector2(size.x * cellSize2dInv.x + pos.x, size.y * cellSize2dInv.y + pos.y);

            var posFl = Vector2Int.FloorToInt(pos);
            var pos2Cl = Vector2Int.CeilToInt(pos2);
            if (posFl.x == pos2Cl.x)
                pos2Cl.x += 1;
            if (posFl.y == pos2Cl.y)
                pos2Cl.y += 1;
            posFl = Vector2Int.Max(Vector2Int.zero, posFl);
            pos2Cl = Vector2Int.Min(mapSize, pos2Cl);

            int cellPosY = posFl.y * sizex;
            #endregion
            for (int y = posFl.y; y < pos2Cl.y; y++, cellPosY += sizex)
            {
                for (int x = posFl.x; x < pos2Cl.x; x++)
                {
                    foreach (var p in cells[cellPosY + x])
                    {
                        if (p.Tag != tag)
                        {
                            p.Tag = tag;
                            if (ksids.IsParentOrEqual(p.Ksid, ksid))
                                output.Add(p);
                        }
                    }
                }
            }
        }

        public void Get(List<Placeable> output, Vector2Int pos, Ksid ksid, ref int tag)
        {
            if (tag == 0)
                tag = GetNextTag();

            if (CellToCell(pos, out var cellPos))
            {
                foreach (var p in cells[cellPos])
                {
                    if (p.Tag != tag)
                    {
                        p.Tag = tag;
                        if (ksids.IsParentOrEqual(p.Ksid, ksid))
                            output.Add(p);
                    }
                }
            }
        }


        public Placeable GetFirst(ref Cell cell, Ksid ksid)
        {
            foreach (var p in cell)
            {
                if (ksids.IsParentOrEqual(p.Ksid, ksid))
                    return p;
            }
            return null;
        }
        public Placeable GetFirst(ref Cell cell, CellFlags flags)
        {
            foreach (var p in cell)
            {
                if ((p.CellBlocking & flags) != 0)
                    return p;
            }
            return null;
        }


        protected bool WorldToCell(Vector2 pos, out int cellPos)
        {
            pos -= mapOffset;
            pos.Scale(cellSize2dInv);
            var posFl = Vector2Int.FloorToInt(pos);
            cellPos = posFl.y * sizex + posFl.x;
            return posFl.x >= 0 && posFl.y >= 0 && posFl.x < mapSize.x && posFl.y < mapSize.y;
        }

        protected bool CellToCell(Vector2Int pos, out int cellPos)
        {
            cellPos = pos.y * sizex + pos.x;
            return pos.x >= 0 && pos.y >= 0 && pos.x < mapSize.x && pos.y < mapSize.y;
        }

        public Vector2Int WorldToCell(Vector2 pos)
        {
            pos -= mapOffset;
            pos.Scale(cellSize2dInv);
            return Vector2Int.FloorToInt(pos);
        }

        public Vector2 CellToWorld(Vector2Int cPoss)
        {
            return new Vector2(cPoss.x * cellSize2D.x, cPoss.y * cellSize2D.y) + mapOffset;
        }

        protected Vector2Int CellToCell(int cellPoss)
        {
            return new Vector2Int(cellPoss % sizex, cellPoss / sizex);
        }

        public bool IsXNearNextCell(float x, int direction)
        {
            float xCell = x * cellSize2dInv.x;
            return (Mathf.FloorToInt(xCell) != Mathf.FloorToInt(xCell + direction * 0.5f));
        }
    }
}
