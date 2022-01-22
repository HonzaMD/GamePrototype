﻿using Assets.Scripts.Core;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Map
{ 
    [Flags]
    public enum CellStateCahnge
    {
        None = 0,
        CompactSand0 = 1,
        CompactSand1 = 2,
        FreeSand0 = 4,
        FreeSand1 = 8,
    }

    public partial class Map
    {
        private readonly Dictionary<int, CellStateCahnge> candidates = new Dictionary<int, CellStateCahnge>();
        private readonly Queue<int> candidatesQ = new Queue<int>();

        private readonly SubCell[] buffer = new SubCell[6*4*2];
        private readonly static Vector2 buffCSize = CellSize2d / 4;
        private readonly static Vector2 buffCSizeInv = new Vector2(1f / buffCSize.x, 1f / buffCSize.y);
        private readonly static Vector2 buffCSizeHalf = CellSize2d / 8;

        private Vector2 buffOffset;
        private Vector2Int buffSize;
        private int buffZshift;
        private int cellPos;
        private Vector2Int cellXY;

        public void AddCellStateTest(Vector2Int cPoss, CellStateCahnge change)
        {
            if (CellToCell(cPoss, out int cellPoss))
            {
                if (!candidates.TryGetValue(cellPoss, out var origChange))
                    candidatesQ.Enqueue(cellPoss);
                if (change != origChange)
                    candidates[cellPoss] = origChange | change;
            }
        }

        public void ProcessCellStateTests(int count)
        {
            for (int f = 0; f < count && candidatesQ.Count > 0; f++)
            {
                cellPos = candidatesQ.Dequeue();
                candidates.Remove(cellPos, out var change);
                ProcessCellStateTest(change);
            }
        }

        private void ProcessCellStateTest(CellStateCahnge change)
        {
            InitBuffer();
            if ((change & CellStateCahnge.CompactSand0) != 0)
                TestCompactSand(0);
            if ((change & CellStateCahnge.CompactSand1) != 0)
                TestCompactSand(1);
            if ((change & CellStateCahnge.FreeSand0) != 0)
                TestFreeSand(0);
            if ((change & CellStateCahnge.FreeSand1) != 0)
                TestFreeSand(1);
            ClearBuffer();
        }

        private void InitBuffer()
        {
            cellXY = CellToCell(cellPos);
            var wPos = CellToWorld(cellXY);
            buffOffset = wPos - new Vector2(buffCSize.x, 0);
            buffSize = new Vector2Int(6, 4);
            InitBuffZShift();
        }

        private void ClearBuffer()
        {
            var size = buffSize.x * buffSize.y * 2;
            for (int f = 0; f < size; f++)
            {
                buffer[f] = default;
            }
        }

        private void InitBuffZShift()
        {
            buffZshift = buffSize.x * buffSize.y;
        }

        private void TestCompactSand(int cellz)
        {
            if (cells[cellPos].Blocking.HasSubFlag(SubCellFlags.Sand, cellz))
                return;
            int tag = GetNextTag();
            MarkCellInBuffer(ref cells[cellPos], cellz, tag);
            if (FastSkip(cellz * buffZshift))
                return;
            MarkFloor(cellz);
            MarkCellInBuffer(ref GetCell(cellXY + Vector2Int.left), cellz, tag);
            MarkCellInBuffer(ref GetCell(cellXY + Vector2Int.right), cellz, tag);
            DoSandTest(cellz * buffZshift);
        }

        private void DoSandTest(int zShift)
        {
            int c = BuffCToBuffC(new Vector2Int(1, 0), zShift);
            int l1 = DoSandTestInRow(c, Content.Speedy);
            if (l1 == 0 || !DoBorderSandTest(c-1, l1))
                return;
            int l2 = DoSandTestInRow(c+1, Content.Speedy | Content.Other);
            if (l2 == 0)
                return;
            int l3 = DoSandTestInRow(c+2, Content.Speedy | Content.Other);
            if (l3 == 0)
                return;
            int l4 = DoSandTestInRow(c+3, Content.Speedy);
            if (l4 == 0 || !DoBorderSandTest(c+4, l4))
                return;

            bool isFullCell = IsFullCell(l1, l2, l3, l4);
            if (!isFullCell)
                MarkNewSand(l1, l2, l3, l4, c);
        }

        private void MarkNewSand(int l1, int l2, int l3, int l4, int c)
        {
            MarkNewSand(l1, c);
            MarkNewSand(l2, c+1);
            MarkNewSand(l3, c+2);
            MarkNewSand(l4, c+3);
        }

        private void MarkNewSand(int l, int c0)
        {
            for (int y = 0 + 4 - l; y < 4; y++)
            {
                int c = c0 + y * buffSize.x;
                buffer[c].Content |= Content.NewSand;
            }
        }

        private static bool IsFullCell(int l1, int l2, int l3, int l4) => l1 == 4 && l2 == 4 && l3 == 4 && l4 == 4;

        private bool DoBorderSandTest(int c0, int l)
        {
            for (int y = 0 + 4-l; y < 3; y++)
            {
                int c = c0 + y * buffSize.x;
                if (!buffer[c].BlockSide)
                    return false;
            }
            return true;
        }

        private int DoSandTestInRow(int c, Content badContent)
        {
            int l = 0;
            if (buffer[c].Stattic)
                l++;
            if ((buffer[c].Content & badContent) != 0)
                l = 0;

            c += buffSize.x;
            if (buffer[c].Stattic || (l > 0 && buffer[c].SandOrBlock))
                l++;
            if ((buffer[c].Content & badContent) != 0)
                l = 0;

            c += buffSize.x;
            if (buffer[c].Stattic || (l > 0 && buffer[c].SandOrBlock))
                l++;
            if ((buffer[c].Content & badContent) != 0)
                l = 0;

            c += buffSize.x;
            if (buffer[c].Stattic || (l > 0 && buffer[c].SandOrBlock))
                l++;
            return l;
        }

        private void MarkCellInBuffer(ref Cell cell, int cellz, int tag)
        {
            foreach (Placeable p in cell)
            {
                if (p.Tag != tag)
                {
                    p.Tag = tag;
                    if (p.CellBlocking.IsPartBlock(cellz))
                    {
                        MarkInBuffer(p, cellz * buffZshift);
                    }
                }
            }
        }

        private bool FastSkip(int zShift)
        {
            int c = BuffCToBuffC(new Vector2Int(1, 3), zShift);
            bool hasSand = false;
            if (!buffer[c].SandOrBlock)
                return true;
            hasSand |= buffer[c].MySand;
            if (!buffer[c+1].SandOrBlock)
                return true;
            hasSand |= buffer[c+1].MySand;
            if (!buffer[c+2].SandOrBlock)
                return true;
            hasSand |= buffer[c+2].MySand;
            if (!buffer[c+3].SandOrBlock)
                return true;
            hasSand |= buffer[c+3].MySand;
            return !hasSand;
        }

        private void MarkFloor(int cellz)
        {
            if (GetCellBlocking(cellXY + Vector2Int.down).HasSubFlag(SubCellFlags.HasFloor, cellz))
            {
                int c = BuffCToBuffC(new Vector2Int(1, 0), cellz * buffZshift);
                buffer[c].Content |= Content.Stattic;
                buffer[c + 1].Content |= Content.Stattic;
                buffer[c + 2].Content |= Content.Stattic;
                buffer[c + 3].Content |= Content.Stattic;
            }
        }

        private void MarkInBuffer(Placeable p, int zShift)
        {
            Content content = ClassifyContent(p);
            if (p.Settings?.UseSimpleBBCollisions == true)
                MarkByBB(p, zShift, content);
            else
                MarkByCP(p, zShift, content);
        }

        private void MarkByCP(Placeable p, int zShift, Content content)
        {
            Mark2x2(p, zShift, content, new Vector2Int(1, 1));
            Mark2x2(p, zShift, content, new Vector2Int(3, 1));
            Mark2x2(p, zShift, content, new Vector2Int(5, 1));
            Mark2x2(p, zShift, content, new Vector2Int(1, 3));
            Mark2x2(p, zShift, content, new Vector2Int(3, 3));
            Mark2x2(p, zShift, content, new Vector2Int(5, 3));
        }

        private void Mark2x2(Placeable p, int zShift, Content content, Vector2Int cell)
        {
            Vector2 point = BuffCToWorld(cell);
            Vector3 point3 = BuffCToWorld(cell).AddZ(zShift == 0 ? 0f : 0.5f);
            Vector2 cPoint = p.GetClosestPoint(point3).XY();

            if (cPoint == point)
            {
                int c = BuffCToBuffC(cell, zShift);
                buffer[c].Content |= content;
                buffer[c-1].Content |= content;
                buffer[c - buffSize.x].Content |= content;
                buffer[c - buffSize.x - 1].Content |= content;
            }
            else if ((point - cPoint).Abs().IsLessEq(buffCSize))
            {
                int c = BuffCToBuffC(cell, zShift);
                if (point.IsLessEq(cPoint) || (!cPoint.IsLessEq(point) && TestCloseHit(p, point3 + (Vector3)buffCSizeHalf)))
                {
                    buffer[c].Content |= content;
                }

                if (point.IsYLessXGrEq(cPoint) || (!cPoint.IsYLessXGrEq(point) && TestCloseHit(p, point3 + new Vector3(-buffCSizeHalf.x, buffCSizeHalf.y, 0))))
                {
                    buffer[c-1].Content |= content;
                }

                if (cPoint.IsLessEq(point) || (!point.IsLessEq(cPoint) && TestCloseHit(p, point3 - (Vector3)buffCSizeHalf)))
                {
                    buffer[c - 1 - buffSize.x].Content |= content;
                }

                if (cPoint.IsYLessXGrEq(point) || (!point.IsYLessXGrEq(cPoint) && TestCloseHit(p, point3 + new Vector3(buffCSizeHalf.x, -buffCSizeHalf.y, 0))))
                {
                    buffer[c - buffSize.x].Content |= content;
                }
            }
        }

        private bool TestCloseHit(Placeable p, Vector3 point)
        {
            Vector3 cPoint = p.GetClosestPoint(point);
            return (point.XY() - cPoint.XY()).Abs().IsLessEq(buffCSizeHalf);
        }

        private void MarkByBB(Placeable p, int zShift, Content content)
        {
            Vector2 shrink = Vector2.Min(new Vector2(0.06f, 0.06f), p.Size) * 0.5f;
            Vector2Int a = WorldToBuffC(p.PlacedPosition + shrink);
            Vector2Int b = WorldToBuffC(p.PlacedPosition + p.Size - shrink);
            a = Vector2Int.Max(Vector2Int.zero, a);
            b = Vector2Int.Min(buffSize - Vector2Int.one, b);

            for (int y = a.y; y <= b.y; y++)
            {
                for (int x = a.x; x <= b.x; x++)
                {
                    int cell = y * buffSize.x + x + zShift;
                    buffer[cell].Content |= content;
                }
            }
        }

        private Content ClassifyContent(Placeable p)
        {
            if (p.Velocity.sqrMagnitude > 0.02f || p.AngularVelocity.sqrMagnitude > 0.05f)
            {
                return Content.Speedy;
            }
            else if (p.Rigidbody != null)
            {
                if (ksids.IsParentOrEqual(p.Ksid, Ksid.SandLike))
                {
                    if (WorldToCell(p.Pivot, out var cp) && cellPos == cp)
                        return Content.SandMy;
                    else
                        return Content.Sand2;
                }
                else
                {
                    return Content.Other;
                }
            }
            else
            {
                return Content.Stattic;
            }
        }

        private void TestFreeSand(int cellz)
        {
            throw new NotImplementedException();
        }

        private struct SubCell
        {
            public Content Content;

            public bool SandOrBlock => (Content & (Content.Sand2 | Content.SandMy | Content.Stattic)) != 0;
            public bool MySand => (Content & Content.SandMy) != 0;
            public bool Stattic => (Content & Content.Stattic) != 0;
            public bool BlockSide => (Content & Content.Stattic) != 0 || ((Content & (Content.Sand2 | Content.Other)) != 0 && (Content & Content.Speedy) == 0);
        }

        [Flags]
        private enum Content
        { 
            None = 0,
            SandMy = 1,
            Sand2 = 2,
            Other = 4,
            Stattic = 8,
            Speedy = 16,
            NewSand = 32,
        }

        private int BuffCToBuffC(Vector2Int pos, int zShift)
        {
            return pos.y * buffSize.x + pos.x + zShift;
        }

        private Vector2Int WorldToBuffC(Vector2 pos)
        {
            pos -= buffOffset;
            pos.Scale(buffCSizeInv);
            return Vector2Int.FloorToInt(pos);
        }

        public Vector2 BuffCToWorld(Vector2Int cPoss)
        {
            return new Vector2(cPoss.x * buffCSize.x, cPoss.y * buffCSize.y) + buffOffset;
        }

    }
}