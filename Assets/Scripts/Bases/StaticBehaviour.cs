using Assets.Scripts.Core;
using Assets.Scripts.Utils;
using System;
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

        /// <summary>
        /// Radeji volej z casovace, at se ti neprovedou kill efekty uvnitr nejakyho slozityho procesingu
        /// </summary>
        private static void ApplyDamage(this Label label, Ksid damageType, float intensity)
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


        public static void ApplyImpactDamage(float impactSpeedSqr, Label myLabel, Label otherLabel, bool isSpring)
        {
            var threashold = isSpring ? PhysicsConsts.ImpactDmgThresholdSpring : PhysicsConsts.ImpactDmgThreshold;
            if (impactSpeedSqr <= threashold)
                return;

            float dmgSpeed = impactSpeedSqr - threashold;

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

            myLabel.ApplyDamageDelayed(Ksid.DamagedByImpact, selfDmg);
            if (otherRB == null || isSpring)
                otherLabel.ApplyDamageDelayed(Ksid.DamagedByImpact, otherDmg);
        }


        public static void ApplyKnifeDamage(float impactSpeedSqr, Label myLabel, Label otherLabel)
        {
            // myLabel je nuz -> damage na otherLabel (jen pokud other nema RB, jinak to vyresi other's callback)
            if (!otherLabel.HasRB)
                ApplyKnifeDamageOneWay(impactSpeedSqr, otherLabel, myLabel);
            ApplyKnifeDamageOneWay(impactSpeedSqr, myLabel, otherLabel);
        }

        public static void ApplyKnifeDamageOneWay(float impactSpeedSqr, Label targetLabel, Label knifeLabel)
        {
            if (knifeLabel.KsidGet.IsChildOf(Ksid.DealsKnifeDamage) && targetLabel.KsidGet.IsChildOf(Ksid.DamagedByKnife)
                && knifeLabel.TryGetComponent(out IKnife knife) && knife.IsActive)
            {
                float dmg = ComputeKnifeDmg(knife, impactSpeedSqr);
                if (dmg > 0)
                    targetLabel.ApplyDamageDelayed(Ksid.DamagedByKnife, dmg);
            }
        }

        private static float ComputeKnifeDmg(IKnife knife, float impactSpeedSqr)
        {
            return knife.GetDmg() * Mathf.Max(PhysicsConsts.KnifeDmgMinFactor,
                Mathf.Sqrt(impactSpeedSqr) / PhysicsConsts.KnifeDmgRefSpeed);
        }
    }
}
