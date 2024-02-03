using Assets.Scripts.Stuff;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Map.Visibility
{

    internal class DarkCaster
    {
        public short Id { get; }
        private readonly VCore core;
        public Vector2Int LeftCell;
        public Vector2Int RightCell;
        public Vector2 LeftPoint, RightPoint;
        public Vector2 LeftDir, RightDir;
        private Occluder Occluder;
        public DCGroup Group;
        public bool connectsLeft;
        public bool connectsRight;
        public readonly List<Vector2Int> cells = new();
        public bool Live;
        public bool Active;

        public override string ToString()
        {
            return $"L:{(connectsLeft?"*":"")} {LeftDir.normalized} R:{(connectsRight ? "*" : "")} {RightDir.normalized} Id:{Id} CellCount:{cells.Count}";
        }

        public DarkCaster(short id, VCore core)
        {
            Id = id;
            this.core = core;
        }

        private bool DirsValid => LeftDir != Vector2.zero && RightDir != Vector2.zero;
        private bool DirsDiverge => VCore.IsBetterOrder(RightDir, LeftDir) || connectsRight || connectsLeft;
        public bool IsReCastable => (connectsRight || connectsLeft) && DirsValid;
        private bool EnoughtBig => cells.Count >= ((connectsLeft || connectsRight) ? 7 : 5);

        Vector2 GroupLeftDir => Group?.LeftDC.LeftDir ?? LeftDir;
        Vector2 GroupRightDir => Group?.RightDC.RightDir ?? RightDir;

        public void Free()
        {
            if (Occluder)
            {
                Occluder.Kill();
                Occluder = null;
            }
            Group?.Free();
            cells.Clear();
            Active = false;
            connectsLeft = false;
            connectsRight = false;
        }

        internal void InitVectors(Vector2 centerPosLocal, Vector2 center, Vector2Int pos)
        {
            cells.Add(pos);
            Active = true;
            LeftCell = pos;
            RightCell = pos;
            core.MarkPartShadowsInNearCells(pos, 2);
            LeftPoint = center;
            RightPoint = center;
            ComputeLeftRightDir(centerPosLocal, center, out LeftDir, out RightDir);
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

        private void TestOtherPoint(Vector2 centerPosLocal, Vector2 a, Vector2Int pos, DarkCaster darkDC, DarkBorders darkBorders)
        {
            Vector2 left, right;
            ComputeLeftRightDir(centerPosLocal, a, out left, out right);
            if (!connectsLeft && (VCore.IsBetterOrder(LeftDir, left) || (LeftDir == Vector2.zero && left != Vector2.zero)))
            {
                LeftPoint = a;
                LeftDir = left;
                SetCell(ref LeftCell, pos);
            }
            if (!connectsRight && (VCore.IsBetterOrder(right, RightDir) || (RightDir == Vector2.zero && right != Vector2.zero)))
            {
                RightPoint = a;
                RightDir = right;
                SetCell(ref RightCell, pos);
            }

            if (darkDC != null) 
            {
                if (right == Vector2.zero)
                    right = a - centerPosLocal;
                var (gl, gr, lOK, rOK) = darkBorders.FindLeftRight(right);
                if (!connectsLeft && rOK && VCore.IsBetterOrder(RightDir, gr) && VCore.IsBetterOrder(LeftDir, gr))
                {
                    if (gr != darkDC.GroupRightDir)
                        throw new InvalidOperationException();
                    //Debug.Assert(gr == darkDC.GroupRightDir);
                    connectsLeft = true;
                    LeftDir = gr;
                }

                if (!connectsRight && lOK && VCore.IsBetterOrder(gl, LeftDir) && VCore.IsBetterOrder(gl, RightDir))
                {
                    if (gl != darkDC.GroupLeftDir)
                        throw new InvalidOperationException();
                    //Debug.Assert(gl == darkDC.GroupLeftDir);
                    connectsRight = true;
                    RightDir = gl;
                }
            }
        }


        private void SetCell(ref Vector2Int cell, Vector2Int pos)
        {
            core.MarkPartShadowsInNearCells(cell, -1);
            core.MarkPartShadowsInNearCells(pos, 1);
            cell = pos;
        }

        internal void Attach(Vector2Int pos, Vector2 centerPosLocal, DarkCaster darkDC, DarkBorders darkBorders)
        {
            cells.Add(pos);
            var center = VCore.CellCenter(pos);
            TestOtherPoint(centerPosLocal, center, pos, darkDC, darkBorders);
            Live = true;
        }

        internal void JoinWith(DarkCaster dcTJ)
        {
            foreach (var cell in dcTJ.cells)
            {
                cells.Add(cell);
            }
            JoinExtendLeft(dcTJ);
            JoinExtendRight(dcTJ);    
            dcTJ.Abandon();
        }

        private void JoinExtendLeft(DarkCaster dcTJ)
        {
            if (!connectsLeft && (VCore.IsBetterOrder(LeftDir, dcTJ.LeftDir) || (LeftDir == Vector2.zero && dcTJ.LeftDir != Vector2.zero) || dcTJ.connectsLeft))
            {
                LeftPoint = dcTJ.LeftPoint;
                LeftDir = dcTJ.LeftDir;
                SetCell(ref LeftCell, dcTJ.LeftCell);
                connectsLeft = dcTJ.connectsLeft;
            }
        }

        private void JoinExtendRight(DarkCaster dcTJ)
        {
            if (!connectsRight && (VCore.IsBetterOrder(dcTJ.RightDir, RightDir) || (RightDir == Vector2.zero && dcTJ.RightDir != Vector2.zero) || dcTJ.connectsRight))
            {
                RightPoint = dcTJ.RightPoint;
                RightDir = dcTJ.RightDir;
                SetCell(ref RightCell, dcTJ.RightCell);
                connectsRight = dcTJ.connectsRight;
            }
        }

        internal void Abandon()
        {
            cells.Clear();
            Live = false;
            Active = false;
        }


        public (bool canCast, bool abandon) CanCast()
        {
            if (Active == false)
                return (false, false);
            if (connectsLeft && connectsRight)
                return (true, false);
            if (Live)
            {
                Live = false;
                return (EnoughtBig && DirsDiverge && DirsValid, false);
            }
            else
            {
                return (DirsDiverge && DirsValid, true);
            }
        }


        internal void CorrectRightDir(Vector2 dir)
        {
            RightDir = dir;
            connectsRight = true;
        }

        internal void CorrectLeftDir(Vector2 dir)
        {
            LeftDir = dir;
            connectsLeft = true;
        }

        internal void InitOccluder(Vector2 posToWorld)
        {
            var occ = Game.Instance.PrefabsStore.Occluder.Create(Game.Instance.OccludersRoot.transform, Vector3.zero);
            occ.Init(this, posToWorld);
            Occluder = occ;
        }

        internal bool GroupEquals(DarkCaster darkCaster) => darkCaster == this || (Group == darkCaster.Group && Group != null);
    }
}
