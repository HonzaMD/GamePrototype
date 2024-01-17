using Assets.Scripts.Stuff;
using UnityEngine;

namespace Assets.Scripts.Map.Visibility
{
    internal class DarkCaster
    {
        public short Id { get; }
        public Vector2Int LeftCell;
        public Vector2Int RightCell;
        public Vector2 LeftPoint, RightPoint;
        public Vector2 LeftDir, RightDir;
        private Occluder Occluder;
        public DarkCaster DCMostLeft;
        public DarkCaster DCMostRight;
        private int cellCount;
        public bool Live;
        public bool Active;

        public DarkCaster(short id)
        {
            Id = id;
        }

        private bool DirsValid => LeftDir != Vector2.zero && RightDir != Vector2.zero;
        private bool DirsDiverge => VCore.IsBetterOrder(RightDir, LeftDir) || DCMostRight != null || DCMostLeft != null;

        public void Free()
        {
            if (Occluder)
            {
                Occluder.Kill();
                Occluder = null;
            }
            cellCount = 0;
            Active = false;
            DCMostLeft = null;
            DCMostRight = null;
        }

        internal void InitVectors(Vector2 centerPosLocal, Vector2 pivot, Vector2Int pos)
        {
            cellCount = 1;
            Active = true;
            LeftCell = pos;
            RightCell = pos;
            LeftPoint = pivot;
            RightPoint = pivot;
            ComputeLeftRightDir(centerPosLocal, pivot, out LeftDir, out RightDir);
            TestOtherPoint(centerPosLocal, pivot + new Vector2(0.5f, 0), pos);
            TestOtherPoint(centerPosLocal, pivot + new Vector2(0.5f, 0.5f), pos);
            TestOtherPoint(centerPosLocal, pivot + new Vector2(0, 0.5f), pos);
            Live = true;
        }

        private void ComputeLeftRightDir(Vector2 centerPosLocal, Vector2 a, out Vector2 leftDir, out Vector2 rightDir)
        {
            Vector2 toA = a - centerPosLocal;
            if (toA.sqrMagnitude < VCore.centerRadiusMarginSq)
            {
                leftDir = Vector2.zero; rightDir = Vector2.zero;
                return;
            }

            var toLeft = VCore.TurnLeft(toA).normalized * VCore.centerRadius;
            leftDir = a - (centerPosLocal + toLeft);
            rightDir = a - (centerPosLocal - toLeft);
        }

        private void TestOtherPoint(Vector2 centerPosLocal, Vector2 a, Vector2Int pos)
        {
            Vector2 left, right;
            ComputeLeftRightDir(centerPosLocal, a, out left, out right);
            if (DCMostLeft == null && VCore.IsBetterOrder(LeftDir, left) || (LeftDir == Vector2.zero && left != Vector2.zero))
            {
                LeftPoint = a;
                LeftDir = left;
                LeftCell = pos;
            }
            if (DCMostRight == null && VCore.IsBetterOrder(right, RightDir) || (RightDir == Vector2.zero && right != Vector2.zero))
            {
                RightPoint = a;
                RightDir = right;
                RightCell = pos;
            }
        }

        internal void Attach(Vector2Int pos, Vector2 centerPosLocal)
        {
            cellCount++;
            var pivot = VCore.CellPivot(pos);
            TestOtherPoint(centerPosLocal, pivot, pos);
            TestOtherPoint(centerPosLocal, pivot + new Vector2(0.5f, 0), pos);
            TestOtherPoint(centerPosLocal, pivot + new Vector2(0.5f, 0.5f), pos);
            TestOtherPoint(centerPosLocal, pivot + new Vector2(0, 0.5f), pos);
            Live = true;
        }

        internal void JoinWith(DarkCaster dcTJ, Vector2 centerPosLocal)
        {
            cellCount += dcTJ.cellCount;
            TestOtherPoint(centerPosLocal, dcTJ.RightPoint, dcTJ.RightCell);
            TestOtherPoint(centerPosLocal, dcTJ.LeftPoint, dcTJ.LeftCell);
            dcTJ.Abandon();
        }

        internal void Abandon()
        {
            cellCount = 0;
            Live = false;
            Active = false;
        }

        internal void TouchShadow(Vector2 centerPosLocal, DarkCaster dc2, Vector2Int pos)
        {
            var center1 = LeftPoint + RightPoint - 2 * centerPosLocal;
            var center2 = dc2.LeftPoint + dc2.RightPoint - 2 * centerPosLocal;
            var pivot = VCore.CellPivot(pos);
            if (DCMostLeft == null && VCore.IsBetterOrder(center1, center2)) // L1 pak R2
            {
                LeftCell = pos;
                LeftDir = dc2.RightDir;
                LeftPoint = pivot;
                DCMostLeft = dc2;

                var normal = VCore.TurnLeft(LeftDir);
                float x = Vector2.Dot(normal, pivot);
                TestTouchPoint(pivot, new Vector2(0.5f, 0), normal, ref x, ref LeftPoint);
                TestTouchPoint(pivot, new Vector2(0, 0.5f), normal, ref x, ref LeftPoint);
                TestTouchPoint(pivot, new Vector2(0.5f, 0.5f), normal, ref x, ref LeftPoint);
            }
            if (DCMostRight == null && VCore.IsBetterOrder(center2, center1)) // L2 pak R1
            {
                RightCell = pos;
                RightDir = dc2.LeftDir;
                RightPoint = pivot;
                DCMostRight = dc2;

                var normal = -VCore.TurnLeft(RightDir);
                float x = Vector2.Dot(normal, pivot);
                TestTouchPoint(pivot, new Vector2(0.5f, 0), normal, ref x, ref RightPoint);
                TestTouchPoint(pivot, new Vector2(0, 0.5f), normal, ref x, ref RightPoint);
                TestTouchPoint(pivot, new Vector2(0.5f, 0.5f), normal, ref x, ref RightPoint);
            }
        }

        private void TestTouchPoint(Vector2 pivot, Vector2 offset, Vector2 normal, ref float x, ref Vector2 output)
        {
            var pivot2 = pivot + offset;
            float x2 = Vector2.Dot(normal, pivot2);
            if (x2 > x)
            {
                x = x2;
                output = pivot2;
            }
        }

        public (bool canCast, bool abandon) CanCast()
        {
            if (Active == false)
                return (false, false);
            if (DCMostLeft != null && DCMostRight != null)
                return (true, false);
            if (Live)
            {
                Live = false;
                return (cellCount >= 5 && DirsDiverge && DirsValid, false);
            }
            else
            {
                return (DirsDiverge && DirsValid, true);
            }
        }

        internal void RemoveCell(Vector2Int pos)
        {
            cellCount--;
            if (cellCount == 0) 
                Active = false;
        }
    }
}
