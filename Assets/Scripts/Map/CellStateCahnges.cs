using Assets.Scripts.Core;
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
        CompactSand = 1,
        FreeSand = 2,
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
                int cellPos = candidatesQ.Dequeue();
                candidates.Remove(cellPos, out var change);
                ProcessCellStateTest(cellPos, change);
            }
        }

        private void ProcessCellStateTest(int cellPos, CellStateCahnge change)
        {
            InitBuffer(cellPos);
            if ((change & CellStateCahnge.CompactSand) != 0)
                TestCompactSand(cellPos);
            if ((change & CellStateCahnge.FreeSand) != 0)
                TestFreeSand(cellPos);
            ClearBuffer();
        }

        private void InitBuffer(int cellPos)
        {
            var wPos = CellToWorld(cellPos);
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

        private void TestCompactSand(int cellPos)
        {
            foreach (Placeable p in cells[cellPos])
            {
                if (p.CellBlocking.IsPartBlock1())
                {
                    MarkInBuffer(p, 0, cellPos);
                }
                if (p.CellBlocking.IsPartBlock2())
                {
                    MarkInBuffer(p, buffZshift, cellPos);
                }
            }
        }

        private void MarkInBuffer(Placeable p, int zShift, int cellPos)
        {
            Content content = ClassifyContent(p, cellPos);
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

        private Content ClassifyContent(Placeable p, int cellPos)
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

        private void TestFreeSand(int cellPos)
        {
            throw new NotImplementedException();
        }

        private struct SubCell
        {
            public Content Content;
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