using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Map.Visibility;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.VFX;

namespace Assets.Scripts.Stuff
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class Occluder : Label
    {
        private bool isAlive;

        public override Placeable PlaceableC => throw new NotSupportedException();
        public override Label Prototype => Game.Instance.PrefabsStore.Occluder;
        public override Ksid KsidGet => Ksid.Unknown;
        public override bool IsAlive => isAlive;

        private DarkCaster dc;

        internal void Init(DarkCaster dc)
        {
            isAlive = true;

            this.dc = dc;
            var line = GetComponentInChildren<LineRenderer>();
            var leftPoint = dc.LeftPoint.AddZ(-0.66f);
            var rightPoint = dc.RightPoint.AddZ(-0.66f);
            var leftDir = dc.LeftDir.normalized.AddZ(0);
            var rightDir = dc.RightDir.normalized.AddZ(0);
            line.SetPosition(0, leftPoint + leftDir * 20f);
            line.SetPosition(1, leftPoint);
            line.SetPosition(2, rightPoint);
            line.SetPosition(3, rightPoint + rightDir * 20f);
        }


        public override void Cleanup()
        {
            isAlive = false;
            dc = null;
            base.Cleanup();
        }
    }
}
