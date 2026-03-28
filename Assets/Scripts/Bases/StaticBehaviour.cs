using Assets.Scripts.Core;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Bases
{
    internal static class StaticBehaviour
    {
        private static Action<Label, Ksid, float> applyDamageAction = ApplyDamage;

        public static void Explode(this Label label)
        {
            Game.Instance.PrefabsStore.Explosion.Create(label.LevelGroup, label.transform.position, null);
            label.Kill();
        }

        public static void ApplyDamageDelayed(this Label label, Ksid damageType, float intensity)
        {
            if (label.KsidGet.IsChildOf(damageType))
            {
                Game.Instance.GlobalTimerHandler.WithKsidFloatParams.Plan(0.3f, applyDamageAction, label, damageType, intensity);
            }
        }

        public static void ApplyDamage(this Label label, Ksid damageType, float intensity)
        {
            var ksids = Game.Instance.Ksids;
            if (ksids.IsParentOrEqual(label.KsidGet, damageType))
            {
                if (ksids.IsParentOrEqual(damageType, Ksid.CausesExplosion) && ksids.IsParentOrEqual(label.KsidGet, Ksid.Explosive))
                {
                    label.Explode();
                }
                else
                {
                    var status = label.GetComponent<Status>();
                    if (status != null)
                    {
                        status.ApplyDamage(damageType, intensity);
                    }
                    else
                    {
                        label.Kill();
                    }
                }
            }
        }


        public static void ApplyImpactDamage(float impactSpeed, Label myLabel, Label otherLabel)
        {
            if (impactSpeed <= PhysicsConsts.ImpactDmgThreshold)
                return;

            float dmgSpeed = impactSpeed - PhysicsConsts.ImpactDmgThreshold;

            var myRB = myLabel.Rigidbody;
            var otherRB = otherLabel.Rigidbody;

            float myMass = myRB != null && !myRB.isKinematic ? myRB.mass : -1;
            float otherMass = otherRB != null && !otherRB.isKinematic ? otherRB.mass : -1;

            float selfDmgFactor, otherDmgFactor;
            if (myMass == -1 || otherMass == -1)
            {
                if (myMass != -1)
                {
                    selfDmgFactor = 1;
                    otherDmgFactor = 0.2f;
                }
                else if (otherMass != -1)
                {
                    selfDmgFactor = 0.2f;
                    otherDmgFactor = 1;
                }
                else
                {
                    selfDmgFactor = otherDmgFactor = 0.5f;
                }
            }
            else
            {
                selfDmgFactor = Mathf.Max(0.2f, otherMass / (myMass + otherMass));
                otherDmgFactor = Mathf.Max(0.2f, myMass / (myMass + otherMass));
            }

            float selfDmg = dmgSpeed * selfDmgFactor * PhysicsConsts.ImpactDmgScale;
            float otherDmg = dmgSpeed * otherDmgFactor * PhysicsConsts.ImpactDmgScale;

            myLabel.ApplyDamage(Ksid.DamagedByImpact, selfDmg);
            if (otherRB == null)
                otherLabel.ApplyDamage(Ksid.DamagedByImpact, otherDmg);
        }
    }
}
