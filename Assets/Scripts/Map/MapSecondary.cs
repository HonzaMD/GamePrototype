using Assets.Scripts.Core;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Assets.Scripts.Map
{
    public class MapSecondary : MapBase<CellSecondary>
    {
        public const int SecondaryMapScale = 9;
        private readonly Map map;
        private readonly Dictionary<Placeable, OBBCellTest> obbCache = new();

        public MapSecondary(int posx, int posy, int sizex, int sizey, Ksids ksids, Map map)
            : base
            (
                  posx / SecondaryMapScale, 
                  posy / SecondaryMapScale, 
                  sizex / SecondaryMapScale, 
                  sizey / SecondaryMapScale, 
                  ksids, 
                  Map.CellSize2d * SecondaryMapScale
            )
        {
            this.map = map;
        }

        public override int GetNextTag() => map.GetNextTag();


        public void AddArea(Placeable p)
        {
            #region Coords Prep CopyPaste
            var pos = p.PlacedPosition - mapOffset;
            pos.Scale(cellSize2dInv);
            var pos2 = new Vector2(p.Size.x * cellSize2dInv.x + pos.x, p.Size.y * cellSize2dInv.y + pos.y);

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
            if (p.Settings.IsOBB)
            {
                var obbTest = new OBBCellTest(Placeable.TempObbC0, Placeable.TempObbC1, Placeable.TempObbC2, Placeable.TempObbC3, cellSize2D);
                obbCache[p] = obbTest;
                AddOBBCells(p, posFl, pos2Cl, cellPosY, ref obbTest);
            }
            else
            {
                for (int y = posFl.y; y < pos2Cl.y; y++, cellPosY += sizex)
                    for (int x = posFl.x; x < pos2Cl.x; x++)
                        cells[cellPosY + x].Add(p, ksids);
            }
        }

        public void AddPoint(Placeable p)
        {
            var wpos = p.PlacedPosition + p.Size * 0.5f;

            if (WorldToCell(wpos, out var cellPos))
            {
                cells[cellPos].Add(p, ksids);
            }
        }

        public void RemoveArea(Placeable p)
        {
            #region Coords Prep CopyPaste
            var pos = p.PlacedPosition - mapOffset;
            pos.Scale(cellSize2dInv);
            var pos2 = new Vector2(p.Size.x * cellSize2dInv.x + pos.x, p.Size.y * cellSize2dInv.y + pos.y);

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
            if (p.Settings.IsOBB)
            {
                var obbTest = obbCache[p];
                obbCache.Remove(p);
                RemoveOBBCells(p, posFl, pos2Cl, cellPosY, ref obbTest);
            }
            else
            {
                for (int y = posFl.y; y < pos2Cl.y; y++, cellPosY += sizex)
                    for (int x = posFl.x; x < pos2Cl.x; x++)
                        cells[cellPosY + x].Remove(p, ksids);
            }
        }

        public void RemovePoint(Placeable p)
        {
            var wpos = p.PlacedPosition + p.Size * 0.5f;

            if (WorldToCell(wpos, out var cellPos))
            {
                cells[cellPos].Remove(p, ksids);
            }
        }


        public void MoveArea(Placeable p, Vector2 placedPositionOld, Vector2 sizeOld)
        {
            var posOld = placedPositionOld - mapOffset;
            posOld.Scale(cellSize2dInv);
            var pos2Old = new Vector2(sizeOld.x * cellSize2dInv.x + posOld.x, sizeOld.y * cellSize2dInv.y + posOld.y);

            var posNew = p.PlacedPosition - mapOffset;
            posNew.Scale(cellSize2dInv);
            var pos2New = new Vector2(p.Size.x * cellSize2dInv.x + posNew.x, p.Size.y * cellSize2dInv.y + posNew.y);


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

        public void MoveAreaOBB(Placeable p, Vector2 placedPositionOld, Vector2 sizeOld, Vector2 posOffsetOld, CellFlags blockingOld)
        {
            var oldTest = obbCache[p];
            var newTest = new OBBCellTest(Placeable.TempObbC0, Placeable.TempObbC1, Placeable.TempObbC2, Placeable.TempObbC3, cellSize2D);

            if (oldTest.SameCorners(newTest) && p.PlacedPosition == placedPositionOld && blockingOld == p.CellBlocking)
            {
                p.PlacedPosition = placedPositionOld;
                p.PosOffset = posOffsetOld;
                p.Size = sizeOld;
                return;
            }

            obbCache[p] = newTest;

            // Full remove with old coords
            var posOld = placedPositionOld - mapOffset;
            posOld.Scale(cellSize2dInv);
            var pos2Old = new Vector2(sizeOld.x * cellSize2dInv.x + posOld.x, sizeOld.y * cellSize2dInv.y + posOld.y);

            var posFlOld = Vector2Int.FloorToInt(posOld);
            var pos2ClOld = Vector2Int.CeilToInt(pos2Old);
            if (posFlOld.x == pos2ClOld.x) pos2ClOld.x += 1;
            if (posFlOld.y == pos2ClOld.y) pos2ClOld.y += 1;
            posFlOld = Vector2Int.Max(Vector2Int.zero, posFlOld);
            pos2ClOld = Vector2Int.Min(mapSize, pos2ClOld);

            RemoveOBBCells(p, posFlOld, pos2ClOld, posFlOld.y * sizex, ref oldTest);

            // Full add with new coords
            var posNew = p.PlacedPosition - mapOffset;
            posNew.Scale(cellSize2dInv);
            var pos2New = new Vector2(p.Size.x * cellSize2dInv.x + posNew.x, p.Size.y * cellSize2dInv.y + posNew.y);

            var posFlNew = Vector2Int.FloorToInt(posNew);
            var pos2ClNew = Vector2Int.CeilToInt(pos2New);
            if (posFlNew.x == pos2ClNew.x) pos2ClNew.x += 1;
            if (posFlNew.y == pos2ClNew.y) pos2ClNew.y += 1;
            posFlNew = Vector2Int.Max(Vector2Int.zero, posFlNew);
            pos2ClNew = Vector2Int.Min(mapSize, pos2ClNew);

            AddOBBCells(p, posFlNew, pos2ClNew, posFlNew.y * sizex, ref newTest);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddOBBCells(Placeable p, Vector2Int posFl, Vector2Int pos2Cl, int cellPosY, ref OBBCellTest obbTest)
        {
            for (int y = posFl.y; y < pos2Cl.y; y++, cellPosY += sizex)
                for (int x = posFl.x; x < pos2Cl.x; x++)
                    if (obbTest.Intersects(x * cellSize2D.x + mapOffset.x, y * cellSize2D.y + mapOffset.y))
                        cells[cellPosY + x].Add(p, ksids);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveOBBCells(Placeable p, Vector2Int posFl, Vector2Int pos2Cl, int cellPosY, ref OBBCellTest obbTest)
        {
            for (int y = posFl.y; y < pos2Cl.y; y++, cellPosY += sizex)
                for (int x = posFl.x; x < pos2Cl.x; x++)
                    if (obbTest.Intersects(x * cellSize2D.x + mapOffset.x, y * cellSize2D.y + mapOffset.y))
                        cells[cellPosY + x].Remove(p, ksids);
        }

        public void MovePoint(Placeable p, Vector2 placedPositionOld, Vector2 sizeOld)
        {
            var wposOld = placedPositionOld + sizeOld * 0.5f;
            var wposNew = p.PlacedPosition + p.Size * 0.5f;

            if (WorldToCell(wposOld, out var cellPosOld))
            {
                if (WorldToCell(wposNew, out var cellPosNew))
                {
                    if (cellPosOld != cellPosNew)
                    {
                        cells[cellPosOld].Remove(p, ksids);
                        cells[cellPosNew].Add(p, ksids);
                    }
                }
                else
                {
                    cells[cellPosOld].Remove(p, ksids);
                }
            }
            else
            {
                if (WorldToCell(wposNew, out var cellPosNew))
                {
                    cells[cellPosNew].Add(p, ksids);
                }
            }
        }
    }
}
