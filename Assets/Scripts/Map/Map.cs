using Assets.Scripts.Core;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Map
{
    public partial class Map
    {
        private readonly int posx;
        private readonly int posy;
        private readonly int sizex;
        private readonly int sizey;
        private readonly Cell[] cells;

        public readonly static Vector3 CellSize = new Vector3(0.5f, 0.5f, 0.5f);
        public readonly static Vector2 CellSize2d = CellSize.XY();
        public readonly static Vector2 CellSize2dInv = new Vector2(1f / CellSize2d.x, 1f / CellSize2d.y);
        public readonly static float CellSizeY3div4 = CellSize.y * 3 / 4;

        private readonly static Vector2 buffCSize = CellSize2d / 4;
        private readonly static Vector2 buffCSizeInv = new Vector2(1f / buffCSize.x, 1f / buffCSize.y);
        private readonly static Vector2 buffCSizeHalf = CellSize2d / 8;


        public readonly Vector2 mapOffset;
        public readonly Vector2Int mapSize;
        private readonly Ksids ksids;
        private int currentTag;

        public int Id { get; }

        public Map(MapSettings settings, Ksids ksids, int id, MapWorlds mapWorlds)
            : this(settings.posx, settings.posy, settings.sizex, settings.sizey, ksids, id, mapWorlds)
        {
        }

        public Map(int posx, int posy, int sizex, int sizey, Ksids ksids, int id, MapWorlds mapWorlds)
        {
            this.posx = posx;
            this.posy = posy;
            this.sizex = sizex;
            this.sizey = sizey;
            mapSize = new Vector2Int(sizex, sizey);
            var mo = new Vector2(posx, posy);
            mo.Scale(CellSize2d);
            mapOffset = mo;

            this.ksids = ksids;
            this.mapWorlds = mapWorlds;
            cells = new Cell[sizex * sizey];
            Id = id;
        }

        public MapSettings Settings => new MapSettings(posx, posy, sizex, sizey);


        public void Add(Placeable p, bool dontRefreshCoordinates = false)
        {
            if (!dontRefreshCoordinates)
            {
                if (p.IsMapPlaced)
                {
                    Debug.LogError("Map Already Placed " + p.name);
                    return;
                }
                p.RefreshCoordinates();
            }
            p.Tag = 0;

            #region Coords Prep CopyPaste
            var pos = p.PlacedPosition - mapOffset;
            pos.Scale(CellSize2dInv);
            var pos2 = new Vector2(p.Size.x * CellSize2dInv.x + pos.x, p.Size.y * CellSize2dInv.y + pos.y);

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
                    cells[cellPosY + x].Add(p, ksids);
                }
            }
        }


        public void Remove(Placeable p)
        {
            if (!p.IsMapPlaced)
                return;
            #region Coords Prep CopyPaste
            var pos = p.PlacedPosition - mapOffset;
            pos.Scale(CellSize2dInv);
            var pos2 = new Vector2(p.Size.x * CellSize2dInv.x + pos.x, p.Size.y * CellSize2dInv.y + pos.y);

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

            LeavingCheck(p, pos, pos2);

            for (int y = posFl.y; y < pos2Cl.y; y++, cellPosY += sizex)
            {
                for (int x = posFl.x; x < pos2Cl.x; x++)
                {
                    cells[cellPosY + x].Remove(p, ksids);
                }
            }

            p.PlacedPosition = Placeable.NotInMap;
        }


        public void Move(Placeable p)
        {
            if (!p.IsMapPlaced)
                throw new InvalidOperationException("Nemuzu pohybovat neco co neni v mape " + p.name);

            var blockingOld = p.CellBlocking;
            var placedPositionOld = p.PlacedPosition;
            var posOIffsetOld = p.PosOffset;
            var sizeOld = p.Size;
            var posOld = p.PlacedPosition - mapOffset;
            posOld.Scale(CellSize2dInv);
            var pos2Old = new Vector2(p.Size.x * CellSize2dInv.x + posOld.x, p.Size.y * CellSize2dInv.y + posOld.y);

            p.RefreshCoordinates();

            if (blockingOld != p.CellBlocking)
            {
                Move2(p, posOld, pos2Old);
                return;
            }

            var posNew = p.PlacedPosition - mapOffset;
            posNew.Scale(CellSize2dInv);
            var pos2New = new Vector2(p.Size.x * CellSize2dInv.x + posNew.x, p.Size.y * CellSize2dInv.y + posNew.y);

            if (posOld == posNew && pos2Old == pos2New)
            {
                p.PlacedPosition = placedPositionOld;
                p.PosOffset = posOIffsetOld;
                p.Size = sizeOld;
                return;
            }

            LeavingCheck(p, posOld, pos2Old);

            var posFlOld = Vector2Int.FloorToInt(posOld);
            var pos2ClOld = Vector2Int.CeilToInt(pos2Old);
            if (posFlOld.x == pos2ClOld.x)
                pos2ClOld.x += 1;
            if (posFlOld.y == pos2ClOld.y)
                pos2ClOld.y += 1;
            posFlOld = Vector2Int.Max(Vector2Int.zero, posFlOld);
            pos2ClOld = Vector2Int.Min(mapSize, pos2ClOld);

            var posFlNew = Vector2Int.FloorToInt(posNew);
            var pos2ClNew = Vector2Int.CeilToInt(pos2New);
            if (posFlNew.x == pos2ClNew.x)
                pos2ClNew.x += 1;
            if (posFlNew.y == pos2ClNew.y)
                pos2ClNew.y += 1;
            posFlNew = Vector2Int.Max(Vector2Int.zero, posFlNew);
            pos2ClNew = Vector2Int.Min(mapSize, pos2ClNew);

            var posFlX = Vector2Int.Max(posFlOld, posFlNew);
            var pos2ClX = Vector2Int.Min(pos2ClOld, pos2ClNew);

            int cellPosY = posFlOld.y * sizex;
            for (int y = posFlOld.y; y < pos2ClOld.y; y++, cellPosY += sizex)
            {
                if (y >= posFlX.y && y < pos2ClX.y)
                {
                    for (int x = posFlOld.x; x < pos2ClOld.x; x++)
                    {
                        if (x < posFlX.x || x >= pos2ClX.x)
                            cells[cellPosY + x].Remove(p, ksids);
                    }
                }
                else
                {
                    for (int x = posFlOld.x; x < pos2ClOld.x; x++)
                    {
                        cells[cellPosY + x].Remove(p, ksids);
                    }
                }
            }

            cellPosY = posFlNew.y * sizex;
            for (int y = posFlNew.y; y < pos2ClNew.y; y++, cellPosY += sizex)
            {
                if (y >= posFlX.y && y < pos2ClX.y)
                {
                    for (int x = posFlNew.x; x < pos2ClNew.x; x++)
                    {
                        if (x < posFlX.x || x >= pos2ClX.x)
                            cells[cellPosY + x].Add(p, ksids);
                    }
                }
                else
                {
                    for (int x = posFlNew.x; x < pos2ClNew.x; x++)
                    {
                        cells[cellPosY + x].Add(p, ksids);
                    }
                }
            }
        }

        private void Move2(Placeable p, Vector2 posOld, Vector2 pos2Old)
        {
            LeavingCheck(p, posOld, pos2Old);

            #region Coords Prep CopyPaste
            var posFlOld = Vector2Int.FloorToInt(posOld);
            var pos2ClOld = Vector2Int.CeilToInt(pos2Old);
            if (posFlOld.x == pos2ClOld.x)
                pos2ClOld.x += 1;
            if (posFlOld.y == pos2ClOld.y)
                pos2ClOld.y += 1;
            posFlOld = Vector2Int.Max(Vector2Int.zero, posFlOld);
            pos2ClOld = Vector2Int.Min(mapSize, pos2ClOld);

            int cellPosY = posFlOld.y * sizex;
            #endregion
            for (int y = posFlOld.y; y < pos2ClOld.y; y++, cellPosY += sizex)
            {
                for (int x = posFlOld.x; x < pos2ClOld.x; x++)
                {
                    cells[cellPosY + x].Remove(p, ksids);
                }
            }

            Add(p, true);
        }

        private void LeavingCheck(Placeable p, Vector2 posOld, Vector2 pos2Old)
        {
            CellFlags sandFilter = CellFlags.Free;
            if (p.CellBlocking.IsPartBlock0())
                sandFilter = CellFlags.Cell0Sand;
            if (p.CellBlocking.IsPartBlock1())
                sandFilter |= CellFlags.Cell1Sand;

            if (sandFilter == CellFlags.Free)
                return;

            #region Coords Prep CopyPaste
            float boundaryX = 0.20f * CellSize2dInv.x;
            var posFlOld = Vector2Int.FloorToInt(posOld - new Vector2(boundaryX, 0));
            var pos2ClOld = Vector2Int.CeilToInt(pos2Old + new Vector2(boundaryX, 0.20f * CellSize2dInv.y));
            posFlOld = Vector2Int.Max(Vector2Int.zero, posFlOld);
            pos2ClOld = Vector2Int.Min(mapSize, pos2ClOld);
            int cellPosY = posFlOld.y * sizex;
            #endregion

            for (int y = posFlOld.y; y < pos2ClOld.y; y++, cellPosY += sizex)
            {
                for (int x = posFlOld.x; x < pos2ClOld.x; x++)
                {
                    var foundSand = cells[cellPosY + x].Blocking & sandFilter;
                    if (foundSand != 0)
                        AddCellStateTest(new Vector2Int(x, y), (CellStateCahnge)foundSand);
                }
            }
        }

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

        public int GetNextTag()
        {
            currentTag++;
            if (currentTag == 0)
                ResetTags();
            return currentTag;
        }

        private void ResetTags()
        {
            currentTag++;
            for (int f = 0; f < cells.Length; f++)
            {
                foreach(var p in cells[f])
                {
                    p.Tag = 0;
                }
            }
        }


        public CellFlags GetCellBlocking(Vector2 pos)
        {
            return WorldToCell(pos, out var cellPos) ? cells[cellPos].Blocking : CellFlags.Free;
        }
        public CellFlags GetCellBlocking(Vector2 pos, Placeable exclude)
        {
            return WorldToCell(pos, out var cellPos) ? cells[cellPos].BlockingExcept(exclude) : CellFlags.Free;
        }
        public CellFlags GetCellBlocking(Vector2Int pos)
        {
            return CellToCell(pos, out var cellPos) ? cells[cellPos].Blocking : CellFlags.Free;
        }
        public CellFlags GetCellBlocking(Vector2Int pos, Placeable exclude)
        {
            return CellToCell(pos, out var cellPos) ? cells[cellPos].BlockingExcept(exclude) : CellFlags.Free;
        }


        public ref Cell GetCell(Vector2 pos)
        {
            if (WorldToCell(pos, out var cellPos))
            {
                return ref cells[cellPos];
            }
            else 
            {
                return ref Cell.Empty;
            }
        }
        public ref Cell GetCell(Vector2Int pos)
        {
            if (CellToCell(pos, out var cellPos))
            {
                return ref cells[cellPos];
            }
            else
            {
                return ref Cell.Empty;
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
            pos.Scale(CellSize2dInv);
            var pos2 = new Vector2(size.x * CellSize2dInv.x + pos.x, size.y * CellSize2dInv.y + pos.y);

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
            pos.Scale(CellSize2dInv);
            var pos2 = new Vector2(size.x * CellSize2dInv.x + pos.x, size.y * CellSize2dInv.y + pos.y);

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


        private bool WorldToCell(Vector2 pos, out int cellPos)
        {
            pos -= mapOffset;
            pos.Scale(CellSize2dInv);
            var posFl = Vector2Int.FloorToInt(pos);
            cellPos = posFl.y * sizex + posFl.x;
            return posFl.x >= 0 && posFl.y >= 0 && posFl.x < mapSize.x && posFl.y < mapSize.y;
        }

        private bool CellToCell(Vector2Int pos, out int cellPos)
        {
            cellPos = pos.y * sizex + pos.x;
            return pos.x >= 0 && pos.y >= 0 && pos.x < mapSize.x && pos.y < mapSize.y;
        }

        public Vector2Int WorldToCell(Vector2 pos)
        {
            pos -= mapOffset;
            pos.Scale(CellSize2dInv);
            return Vector2Int.FloorToInt(pos);
        }

        public Vector2 CellToWorld(Vector2Int cPoss)
        {
            return new Vector2(cPoss.x * CellSize2d.x, cPoss.y * CellSize2d.y) + mapOffset;
        }

        private Vector2Int CellToCell(int cellPoss)
        {
            return new Vector2Int(cellPoss % sizex, cellPoss / sizex);
        }

        public bool IsXNearNextCell(float x, int direction)
        {
            float xCell = x * CellSize2dInv.x;
            return (Mathf.FloorToInt(xCell) != Mathf.FloorToInt(xCell + direction * 0.5f));
        }
    }
}
