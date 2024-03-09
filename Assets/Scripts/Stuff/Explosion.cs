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
    public class Explosion : Label, ISimpleTimerConsumer
    {
        private ActivityTag4 activityTag;

        public override Placeable PlaceableC => throw new NotSupportedException();
        public override Label Prototype => Game.Instance.PrefabsStore.Explosion;
        public override Ksid KsidGet => Ksid.Explosion;
        public override bool IsAlive => activityTag.IsActive;

        public int ActiveTag { get => activityTag.Tag; set => activityTag.Tag = value; }

        public override void Init(Map.Map map) => this.Plan(0.1f);

        public void OnTimer()
        {
            if (activityTag.State == 1)
            {
                ApplyExplosionEffects();
                this.Plan(6);
            }
            else
            {
                Kill();
            }
        }

        private void ApplyExplosionEffects()
        {
            var placeables = ListPool<Placeable>.Rent();
            var size = new Vector2(1f, 1f);
            const float sizeSq = 1.2f * 1.2f;
            const float sizeSq2 = 0.9f * 0.9f;
            Map.Map map = Game.MapFromPos(Pivot.x);
            map.Get(placeables, Pivot - size, size * 2, Ksid.AffectedByExplosion);
            foreach (Placeable p in placeables)
                ApplyExplosionEffects(p, sizeSq, sizeSq2);
            placeables.Return();
        }

        private void ApplyExplosionEffects(Placeable p, float sizeSq, float sizeSq2)
        {
            if (p.gameObject.activeInHierarchy)
            {
                var direction = p.GetClosestPoint(transform.position) - transform.position;
                var distanceSq = direction.sqrMagnitude;
                const float forceIntensity = 10;
                const float explMassConst = 10;
                const float explMassLin = 20;
                if (distanceSq < sizeSq)
                {
                    p.ApplyVelocity(new Vector3((sizeSq - direction.x * direction.x) * Mathf.Sign(direction.x) * forceIntensity, (sizeSq - direction.y * direction.y) * Mathf.Sign(direction.y) * forceIntensity, 0),
                        explMassConst + explMassLin * (sizeSq - distanceSq), VelocityFlags.IsImpact);
                }
                if (distanceSq < sizeSq2)
                {
                    p.ApplyDamageDelayed(Ksid.DamagedByExplosion, sizeSq2 - distanceSq);
                }
            }
        }

        public override void Cleanup()
        {
            activityTag.Reset();
            base.Cleanup();
        }
    }
}
