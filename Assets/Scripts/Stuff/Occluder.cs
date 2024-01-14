using Assets.Scripts.Bases;
using Assets.Scripts.Core;
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

        public void Init()
        {
            isAlive = true;
        }


        public override void Cleanup()
        {
            isAlive = false;
            base.Cleanup();
        }
    }
}
