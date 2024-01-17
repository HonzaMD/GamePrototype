using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static UnityEditor.PlayerSettings;

namespace Assets.Scripts.Map.Visibility
{
    internal class DCManager
    {
        private readonly List<DarkCaster> darkCasters = new();
        private readonly List<DarkCaster> liveDarkCasters = new();
        private readonly VCore visibility;
        private int dcTop;

        private short dcToAttach;
        private short dcToJoin;
        private short dcShadow;
        private CState recolorState;
        private Queue<Vector2Int> workPositions = new();

        private readonly NeighbourTest findActiveCasterAction;
        private readonly NeighbourTest joinOtherCasterAction;
        private readonly NeighbourTest findTouchingShadowAction;
        private readonly NeighbourTest recolorDCAction;
        private readonly NeighbourTest drawShadowAction;

        public DCManager(VCore visibility)
        {
            this.visibility = visibility;
            findActiveCasterAction = FindActiveCaster;
            joinOtherCasterAction = JoinOtherCaster;
            findTouchingShadowAction = FindTouchingShadow;
            recolorDCAction = RecolorDC;
            drawShadowAction = DrawShadow;
        }

        public void FreeDarkCasters()
        {
            for (int i = 0; i < dcTop; i++)
            {
                darkCasters[i].Free();
            }
            dcTop = 0;
            liveDarkCasters.Clear();
        }

        public void AddDarkCandidate(Vector2Int pos)
        {
            dcToAttach = -1;
            visibility.Test8(pos, findActiveCasterAction);
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
            darkCasters[dcToAttach].Attach(pos, visibility.centerPosLocal);
            FindTouchingShadow(pos);
        }

        private void InitAttachedCell(Vector2Int pos)
        {
            ref var cell = ref visibility.Get(pos);
            cell.state = CState.DarkCandidate;
            cell.darkCaster = dcToAttach;
        }

        private void JoinWith(Vector2Int pos, ref Cell cell)
        {
            dcToJoin = cell.darkCaster;
            JoinOtherCaster(pos, ref cell);

            while (workPositions.Count > 0)
            {
                visibility.Test8(workPositions.Dequeue(), joinOtherCasterAction);
            }

            var dcTJ = darkCasters[dcToJoin];
            var dc = darkCasters[dcToAttach];
            dc.JoinWith(dcTJ, visibility.centerPosLocal);
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
            dc.InitVectors(visibility.centerPosLocal, VCore.CellPivot(pos), pos);
            FindTouchingShadow(pos);
        }

        private void FindTouchingShadow(Vector2Int pos)
        {
            dcShadow = -1;
            visibility.Test8(pos, findTouchingShadowAction);
            if (dcShadow != -1)
            {
                darkCasters[dcToAttach].TouchShadow(visibility.centerPosLocal, darkCasters[dcShadow], pos);
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
            ref var cell = ref visibility.Get(pos);

            RecolorDC(pos, ref cell);

            while (workPositions.Count > 0)
            {
                visibility.Test8(workPositions.Dequeue(), recolorDCAction);
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

        public void TryCastDark()
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
                    if (dc.DCMostLeft == null)
                        DrawLine(dc.LeftPoint, dc.LeftDir, dc.Id, -1);
                    if (dc.DCMostRight == null)
                        DrawLine(dc.RightPoint, dc.RightDir, dc.Id, 1);
                    DrawShadow(dc.Id);
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

        private void DrawLine(Vector2 point, Vector2 dir, short dc, int right)
        {
            dcShadow = dc;
            Vector2Int primaryDir;
            Vector2Int secondaryDir;
            float xStep;
            if (MathF.Abs(dir.x) > MathF.Abs(dir.y))
            {
                primaryDir = new Vector2Int(MathF.Sign(dir.x), 0);
                secondaryDir = new Vector2Int(0, MathF.Sign(dir.y));
                xStep = MathF.Abs(dir.y / dir.x);
            }
            else
            {
                secondaryDir = new Vector2Int(MathF.Sign(dir.x), 0);
                primaryDir = new Vector2Int(0, MathF.Sign(dir.y));
                xStep = MathF.Abs(dir.x / dir.y);
            }
            Vector2Int insideDir = VCore.TurnLeft(primaryDir) * right;
            Vector2Int pos;

            if (secondaryDir == Vector2Int.zero)
            {
                point = point + (Vector2)primaryDir * 0.1f + (Vector2)insideDir * 0.1f;
                pos = Vector2Int.FloorToInt(point * 2);
            }
            else
            {
                point = point + (Vector2)primaryDir * 0.1f + (Vector2)secondaryDir * 0.1f;
                pos = Vector2Int.FloorToInt(point * 2) + insideDir;
            }

            float dx = 0;

            while (VCore.CellValid(pos))
            {
                ref var cell = ref visibility.Get(pos);
                if (cell.state == CState.DarkCandidate)
                {
                    darkCasters[cell.darkCaster].TouchShadow(visibility.centerPosLocal, darkCasters[dc], pos - insideDir);
                    darkCasters[cell.darkCaster].RemoveCell(pos);
                }
                cell.state = CState.Dark;
                cell.darkCaster = dc;
                //var insidePos = pos + insideDir;
                //if (CellValid(insidePos))
                //{
                //    ref var cellInside = ref Get(insidePos);
                //    if (cellInside.state == CState.Unknown)
                //        DrawShadow(insidePos, ref Get(insidePos));
                //}

                pos += primaryDir;
                dx += xStep;
                if (dx >= 1)
                {
                    dx -= 1;
                    pos += secondaryDir;
                }
            }
        }

        private void DrawShadow(short dc)
        {
            dcShadow = dc;
            while (workPositions.Count > 0)
            {
                visibility.Test4(workPositions.Dequeue(), drawShadowAction);
            }
        }

        private void DrawShadow(Vector2Int pos, ref Cell cell)
        {
            if (cell.state != CState.Dark)
            {
                if (cell.state == CState.DarkCandidate)
                {
                    darkCasters[cell.darkCaster].RemoveCell(pos);
                }
                cell.state = CState.Dark;
                cell.darkCaster = dcShadow;
                workPositions.Enqueue(pos);
            }
        }
    }
}
