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
    public class ParticleEffect : Label, ISimpleTimerConsumer
    {
        public override Placeable PlaceableC => throw new NotSupportedException();
        public override Label Prototype => Game.Instance.PrefabsStore.ParticleEffect;
        public override Ksid KsidGet => Ksid.ParticleEffect;

        public int ActiveTag { get; set; }

        public void Init(float duration)
        {
            this.Plan(duration);
        }

        public void OnTimer()
        {
            Kill();
        }

        public override void Cleanup()
        {
            if ((ActiveTag & 1) != 0)
                ActiveTag++;

            base.Cleanup();
        }
    }
}
