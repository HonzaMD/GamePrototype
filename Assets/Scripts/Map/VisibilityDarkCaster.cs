using Assets.Scripts.Stuff;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static UnityEditor.PlayerSettings;

namespace Assets.Scripts.Map
{
    public partial class Visibility
    {
        private readonly List<DarkCaster> darkCasters = new();
        private readonly List<DarkCaster> liveDarkCasters = new();
        private int dcTop;

        private short dcToAttach;
        private short dcToJoin;
        private short dcShadow;
        private CState recolorState;
        private Queue<Vector2Int> workPositions = new();

        private class DarkCaster
        {
            public short Id { get; }
            public Vector2Int LeftCell;
            private Vector2Int RightCell;
            private Vector2 LeftPoint, RightPoint;
            private Vector2 LeftDir, RightDir;
            private Occluder Occluder;
            private DarkCaster DCMostLeft;
            private DarkCaster DCMostRight;
            private int cellCount;
            public bool Live;
            public bool Active;

            public DarkCaster(short id)
            {
                Id = id;
            }

            private bool DirsValid => LeftDir != Vector2.zero && RightDir != Vector2.zero;
            private bool DirsDiverge => IsBetterOrder(RightDir, LeftDir) || DCMostRight != null || DCMostLeft != null;

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
                if (toA.sqrMagnitude < centerRadiusMarginSq)
                {
                    leftDir = Vector2.zero; rightDir = Vector2.zero;
                    return;
                }

                var toLeft = TurnLeft(toA).normalized * centerRadius;
                leftDir = a - (centerPosLocal + toLeft);
                rightDir = a - (centerPosLocal - toLeft);
            }

            private void TestOtherPoint(Vector2 centerPosLocal, Vector2 a, Vector2Int pos)
            {
                Vector2 left, right;
                ComputeLeftRightDir(centerPosLocal, a, out left, out right);
                if (DCMostLeft == null && IsBetterOrder(LeftDir, left) || (LeftDir == Vector2.zero && left != Vector2.zero))
                {
                    LeftPoint = a;
                    LeftDir = left;
                    LeftCell = pos;
                }
                if (DCMostRight == null && IsBetterOrder(right, RightDir) || (RightDir == Vector2.zero && right != Vector2.zero))
                {
                    RightPoint = a;
                    RightDir = right;
                    RightCell = pos;
                }
            }

            internal void Attach(Vector2Int pos, Vector2 centerPosLocal)
            {
                cellCount++;
                var pivot = CellPivot(pos);
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
                var pivot = CellPivot(pos);
                if (DCMostLeft == null && IsBetterOrder(center1, center2)) // L1 pak R2
                {
                    LeftCell = pos;
                    LeftDir = dc2.RightDir;
                    LeftPoint = pivot;
                    DCMostLeft = dc2;

                    var normal = TurnLeft(LeftDir);
                    float x = Vector2.Dot(normal, pivot);
                    TestTouchPoint(pivot, new Vector2(0.5f, 0), normal, ref x, ref LeftPoint);
                    TestTouchPoint(pivot, new Vector2(0, 0.5f), normal, ref x, ref LeftPoint);
                    TestTouchPoint(pivot, new Vector2(0.5f, 0.5f), normal, ref x, ref LeftPoint);
                }
                if (DCMostRight == null && IsBetterOrder(center2, center1)) // L2 pak R1
                {
                    RightCell = pos;
                    RightDir = dc2.LeftDir;
                    RightPoint = pivot;
                    DCMostRight = dc2;

                    var normal = -TurnLeft(RightDir);
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
        }

        private void FreeDarkCasters()
        {
            for (int i = 0; i < dcTop; i++)
            {
                darkCasters[i].Free();
            }
            dcTop = 0;
            liveDarkCasters.Clear();
        }

        private void AddDarkCandidate(Vector2Int pos)
        {
            dcToAttach = -1;
            Test8(pos, findActiveCasterAction);
            if (dcToAttach == -1)
            {
                CreateNew(pos);
            }
            else
            {
                Attach(pos);
            }
        }


        private void FindActiveCaster(Vector2Int pos, ref Cell cell)
        {
            if (cell.state == CState.DarkCandidate)
            {
                if (dcToAttach == -1)
                {
                    dcToAttach = cell.darkCaster;
                }
                else if (cell.darkCaster != dcToAttach)
                {
                    JoinWith(pos, ref cell);
                }
            }
        }

        private void Attach(Vector2Int pos)
        {
            InitAttachedCell(pos);
            darkCasters[dcToAttach].Attach(pos, centerPosLocal);
            FindTouchingShadow(pos);
        }

        private void InitAttachedCell(Vector2Int pos)
        {
            ref var cell = ref Get(pos);
            cell.state = CState.DarkCandidate;
            cell.darkCaster = dcToAttach;
        }

        private void JoinWith(Vector2Int pos, ref Cell cell)
        {
            dcToJoin = cell.darkCaster;
            JoinOtherCaster(pos, ref cell);

            while (workPositions.Count > 0)
            {
                Test8(workPositions.Dequeue(), joinOtherCasterAction);
            }

            var dcTJ = darkCasters[dcToJoin];
            var dc = darkCasters[dcToAttach];
            dc.JoinWith(dcTJ, centerPosLocal);
        }

        private void JoinOtherCaster(Vector2Int pos, ref Cell cell)
        {
            if (cell.state == CState.DarkCandidate && cell.darkCaster == dcToJoin)
            {
                cell.darkCaster = dcToAttach;
                workPositions.Enqueue(pos);
            }
        }

        private void CreateNew(Vector2Int pos)
        {
            var dc = CreateDC();
            InitAttachedCell(pos);
            dc.InitVectors(centerPosLocal, CellPivot(pos), pos);
            FindTouchingShadow(pos);
        }

        private void FindTouchingShadow(Vector2Int pos)
        {
            dcShadow = -1;
            Test8(pos, findTouchingShadowAction);
            if (dcShadow != -1)
            {
                darkCasters[dcToAttach].TouchShadow(centerPosLocal, darkCasters[dcShadow], pos);
            }
        }

        private void FindTouchingShadow(Vector2Int pos, ref Cell cell)
        {
            if (cell.state == CState.Dark)
                dcShadow = cell.darkCaster;
        }

        private void RecolorDC(short dc, CState state)
        {
            dcToJoin = dc;
            recolorState = state;
            var pos = darkCasters[dc].LeftCell;
            ref var cell = ref Get(pos);

            RecolorDC(pos, ref cell);

            while (workPositions.Count > 0)
            {
                Test8(workPositions.Dequeue(), recolorDCAction);
            }
        }

        private void RecolorDC(Vector2Int pos, ref Cell cell)
        {
            if (cell.darkCaster == dcToJoin && cell.state == CState.DarkCandidate)
            {
                cell.state = recolorState;
                workPositions.Enqueue(pos);
            }
        }

        private DarkCaster CreateDC()
        {
            dcToAttach = (short)dcTop;
            dcTop++;
            if (darkCasters.Count <= dcToAttach)
                darkCasters.Add(new(dcToAttach));
            liveDarkCasters.Add(darkCasters[dcToAttach]);
            return darkCasters[dcToAttach];
        }

        private void TryCastDark()
        {
            for (int i = 0; i < liveDarkCasters.Count; )
            {
                var dc = liveDarkCasters[i];
                (bool canCast, bool abandon) = dc.CanCast();
                if (!canCast && abandon)
                {
                    RecolorDC(dc.Id, CState.FullShadow);
                    dc.Abandon();
                }
                else if (canCast)
                {
                    RecolorDC(dc.Id, CState.Dark);
                    dc.Abandon();
                }

                if (dc.Active)
                {
                    i++;
                }
                else
                {
                    liveDarkCasters[i] = liveDarkCasters[liveDarkCasters.Count - 1];
                    liveDarkCasters.RemoveAt(liveDarkCasters.Count-1);
                }
            }
        }


        private static bool IsBetterOrder(Vector2 first, Vector2 second) => first.x * second.y - first.y * second.x > 0;
        private static Vector2 TurnLeft(Vector2 vec) => new Vector2(-vec.y, vec.x);
    }
}
