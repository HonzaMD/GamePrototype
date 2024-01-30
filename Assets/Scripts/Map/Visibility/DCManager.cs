using Assets.Scripts.Utils;
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
        private int dcTop;
        private readonly List<DarkCaster> liveDarkCasters = new();
        private readonly VCore core;
        private readonly DarkBorders darkBorders;
        private short dcToAttach;
        private short dcShadow;
        private Vector2Int shadowPos;

        private readonly NeighbourTest findActiveCasterAction;
        private readonly NeighbourTest findTouchingShadowAction;

        public DCManager(VCore core, DarkBorders darkBorders)
        {
            this.core = core;
            this.darkBorders = darkBorders;
            findActiveCasterAction = FindActiveCaster;
            findTouchingShadowAction = FindTouchingShadow;
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
            core.Test8(pos, findActiveCasterAction);
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
                    JoinWith(ref cell);
                }
            }
        }

        private void Attach(Vector2Int pos)
        {
            InitAttachedCell(pos);
            darkCasters[dcToAttach].Attach(pos, core.centerPosLocal, null, null);
            FindTouchingShadow(pos);
        }

        private void InitAttachedCell(Vector2Int pos)
        {
            ref var cell = ref core.Get(pos);
            cell.state = CState.DarkCandidate;
            cell.darkCaster = dcToAttach;
        }

        private void JoinWith(ref Cell cell)
        {
            var dcToJoin = cell.darkCaster;
            var dcTJ = darkCasters[dcToJoin];
            var dc = darkCasters[dcToAttach];

            RecolorJoinedCells(dcTJ, dcToJoin);

            dc.JoinWith(dcTJ);
        }

        private void RecolorJoinedCells(DarkCaster dcTJ, short dcToJoin)
        {
            foreach (var cellPos in dcTJ.cells)
            {
                ref var cell = ref core.Get(cellPos);
                if (cell.state == CState.DarkCandidate && cell.darkCaster == dcToJoin)
                {
                    cell.darkCaster = dcToAttach;
                }
            }
        }

        private void CreateNew(Vector2Int pos)
        {
            var dc = CreateDC();
            InitAttachedCell(pos);
            dc.InitVectors(core.centerPosLocal, VCore.CellCenter(pos), pos);
            FindTouchingShadow(pos);
        }

        private void FindTouchingShadow(Vector2Int pos)
        {
            dcShadow = -1;
            core.Test4(pos, findTouchingShadowAction);
            if (dcShadow != -1 && !GroupEquals(dcShadow, dcToAttach))
            {
                var dc = darkCasters[dcToAttach];
                dc.Attach(shadowPos, core.centerPosLocal, darkCasters[dcShadow], darkBorders);
                //if (dc.IsReCastable) 
                //    darkBorders.Add(dc);
            }
        }

        private bool GroupEquals(short dc1, short dc2) => darkCasters[dc1].GroupEquals(darkCasters[dc2]);

        private void FindTouchingShadow(Vector2Int pos, ref Cell cell)
        {
            if (cell.state == CState.Dark)
            {
                dcShadow = cell.darkCaster;
                shadowPos = pos;
            }
        }

        private void RecolorFinishedCells(DarkCaster dc, short dcId, CState state)
        {
            foreach (var cellPos in dc.cells)
            {
                ref var cell = ref core.Get(cellPos);
                if (cell.state == CState.DarkCandidate && cell.darkCaster == dcId)
                {
                    cell.state = state;
                }
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
                    RecolorFinishedCells(dc, dc.Id, CState.FullShadow);
                    dc.Abandon();
                }
                else if (canCast)
                {
                    RecolorFinishedCells(dc, dc.Id, CState.Dark);
                    darkBorders.Add(dc);
                    dc.InitOccluder(core.posToWorld);
                    dc.Abandon();
                }
                else if (dc.IsReCastable)
                {
                    darkBorders.Add(dc);
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

    }
}
