using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Map
{
    public partial class Map : MapBase<Cell>
    {
        public readonly static Vector3 CellSize = new Vector3(0.5f, 0.5f, 0.5f);
        public readonly static Vector2 CellSize2d = CellSize.XY();
        public readonly static Vector2 CellSize2dInv = new Vector2(1f / CellSize2d.x, 1f / CellSize2d.y);
        public readonly static float CellSizeY3div4 = CellSize.y * 3 / 4;

        private readonly static Vector2 buffCSize = CellSize2d / 4;
        private readonly static Vector2 buffCSizeInv = new Vector2(1f / buffCSize.x, 1f / buffCSize.y);
        private readonly static Vector2 buffCSizeHalf = CellSize2d / 8;

        private int currentTag;

        public int Id { get; }

        private MapSecondary[] mapsSec;

        public RegionMap LightVariantMap { get; } = new();
        public List<ReflectionProbe> ReflectionProbes { get; } = new ();
        public WorldBuilder WorldBuilder { get; set; }


        public Map(MapSettings settings, Ksids ksids, int id, MapWorlds mapWorlds)
            : this(settings.posx, settings.posy, settings.sizex, settings.sizey, ksids, id, mapWorlds)
        {
        }

        public Map(int posx, int posy, int sizex, int sizey, Ksids ksids, int id, MapWorlds mapWorlds)
            : base(posx, posy, sizex, sizey, ksids, CellSize2d)
        {
            this.mapWorlds = mapWorlds;
            Id = id;

            mapsSec = new MapSecondary[(int)SecondaryMap.Last];
            for (int f = 1; f < mapsSec.Length; f++)
                mapsSec[f] = new(posx, posy, sizex, sizey, ksids, this);
        }

        public MapSecondary Secondary(SecondaryMap map) => mapsSec[(int)map];


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

                if (p.Settings.SecondaryMapIndex != SecondaryMap.None)
                {
                    if (p.IsTrigger)
                    {
                        p.Tag = 0;
                        Secondary(p.Settings.SecondaryMapIndex).AddArea(p);
                        return;
                    }
                    else
                    {
                        Secondary(p.Settings.SecondaryMapIndex).AddPoint(p);
                    }
                }
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

            if (p.Settings.SecondaryMapIndex != SecondaryMap.None)
            {
                if (p.IsTrigger)
                {
                    Secondary(p.Settings.SecondaryMapIndex).RemoveArea(p);
                    p.PlacedPosition = Placeable.NotInMap;
                    return;
                }
                else
                {
                    Secondary(p.Settings.SecondaryMapIndex).RemovePoint(p);
                }
            }


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
                if (TrySecondaryMove(p, placedPositionOld, sizeOld))
                    return;
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

            if (TrySecondaryMove(p, placedPositionOld, sizeOld))
                return;

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

        private bool TrySecondaryMove(Placeable p, Vector2 placedPositionOld, Vector2 sizeOld)
        {
            if (p.Settings.SecondaryMapIndex != SecondaryMap.None)
            {
                if (p.IsTrigger)
                {
                    Secondary(p.Settings.SecondaryMapIndex).MoveArea(p, placedPositionOld, sizeOld);
                    return true;
                }
                else
                {
                    Secondary(p.Settings.SecondaryMapIndex).MovePoint(p, placedPositionOld, sizeOld);
                }
            }
            return false;
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


        public override int GetNextTag()
        {
            currentTag++;
            if (currentTag == 0)
            {
                currentTag++;
                ResetTags();
                for (int i = 1; i < mapsSec.Length; i++)
                    mapsSec[i].ResetTags();
            }
            return currentTag;
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
    }
}
