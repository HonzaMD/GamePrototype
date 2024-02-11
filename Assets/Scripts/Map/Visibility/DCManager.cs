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
        private readonly List<DarkCaster> darkCasters = new() { null}; // ID 0 nevyuzivam
        private int dcTop = 1;
        private readonly List<DarkCaster> liveDarkCasters = new();
        private readonly VCore core;
        private readonly DarkBorders darkBorders;
        private short dcToAttach;
        private short dcShadow;
        private Vector2Int shadowPos;

        private readonly NeighbourTest findSeedAction;
        private readonly NeighbourTest findTouchingShadowAction;

        public int dcAbandonCounter;
        public int dcCreateCounter;
        public int dcBorderAddCounter;

        private readonly MeshBuilderWorker meshBuilder;

        public DCManager(VCore core, DarkBorders darkBorders)
        {
            meshBuilder = new(core);
            this.core = core;
            this.darkBorders = darkBorders;
            findSeedAction = FindSeed;
            findTouchingShadowAction = FindTouchingShadow;
        }

        public DarkCaster GetDc(short id) => darkCasters[id];

        public void FreeDarkCasters()
        {
            for (int i = 1; i < dcTop; i++)
            {
                darkCasters[i].Free();
            }
            dcTop = 1;
            liveDarkCasters.Clear();

            dcAbandonCounter = 0;
            dcCreateCounter = 0;
            dcBorderAddCounter = 0;
        }

        public void AddDarkCandidate(Vector2Int pos)
        {
            dcToAttach = -1;
            core.Test8(pos, findSeedAction);
            if (dcToAttach == -1)
            {
                CreateNew(pos);
            }
            else
            {
                Attach(pos);
            }
        }


        private void FindSeed(Vector2Int pos, ref Cell cell)
        {
            if (cell.state == CState.DSeed)
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
            InitSeedCell(pos);
            darkCasters[dcToAttach].Attach(pos, core.centerPosLocal, null, null);
            FindTouchingShadow(pos);
        }

        private void InitSeedCell(Vector2Int pos)
        {
            ref var cell = ref core.Get(pos);
            cell.state = CState.DSeed;
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
                if (cell.state == CState.DSeed && cell.darkCaster == dcToJoin)
                {
                    cell.darkCaster = dcToAttach;
                }
            }
        }

        private void CreateNew(Vector2Int pos)
        {
            var dc = CreateDC();
            InitSeedCell(pos);
            dc.InitVectors(core.centerPosLocal, VCore.CellCenter(pos), pos);
            FindTouchingShadow(pos);
        }

        private void FindTouchingShadow(Vector2Int pos)
        {
            dcShadow = -1;
            core.Test4(pos, findTouchingShadowAction);
            if (dcShadow != -1)
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
            if (cell.state == CState.Dark && dcShadow == -1 && !GroupEquals(cell.darkCaster, dcToAttach))
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
                if (cell.state is CState.DSeed or CState.Dark && cell.darkCaster == dcId)
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
                darkCasters.Add(new(dcToAttach, core));
            liveDarkCasters.Add(darkCasters[dcToAttach]);
            return darkCasters[dcToAttach];
        }

        public bool TryCastDark(System.Diagnostics.Stopwatch swCreateDcs)
        {
            swCreateDcs.Start();
            bool bordersChanged = false;
            for (int i = 0; i < liveDarkCasters.Count; )
            {
                var dc = liveDarkCasters[i];
                (bool canCast, bool abandon) = dc.CanCast();
                if (!canCast && abandon)
                {
                    RecolorFinishedCells(dc, dc.Id, CState.FullShadow);
                    dc.Abandon();
                    dcAbandonCounter++;
                }
                else if (canCast)
                {
                    bool added = darkBorders.Add(dc) || dc.IsReCastable;
                    RecolorFinishedCells(dc, dc.Id, added ? CState.Dark : CState.FullShadow);
                    if (added)
                    {
                        dc.InitOccluder(core.posToWorld);
                        bordersChanged = true;
                        dcCreateCounter++;
                    }
                    else
                    {
                        dcAbandonCounter++;
                    }
                    dc.Abandon();
                }
                else if (dc.IsReCastable)
                {
                    darkBorders.Add(dc);
                    bordersChanged = true;
                    dcBorderAddCounter++;
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
            swCreateDcs.Stop();
            return bordersChanged;
        }

        public void BuildMeshes(Vector2 centerPosLocal)
        {
            for (int i = 1; i < dcTop; i++)
            {
                if (darkCasters[i].Occluder != null)
                {
                    var meshFilter = darkCasters[i].Occluder.GetComponent<MeshFilter>();
                    var mesh = meshFilter.sharedMesh;
                    if (mesh == null)
                    {
                        mesh = new Mesh() { name = "Procedural Mesh" };
                        meshFilter.mesh = mesh;
                    }

                    meshBuilder.Build(darkCasters[i], dcTop, mesh, centerPosLocal);
                }
            }
        }
    }
}
