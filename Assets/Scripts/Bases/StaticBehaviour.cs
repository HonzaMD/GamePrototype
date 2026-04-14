using Assets.Scripts.Core;
using Assets.Scripts.Map;
using Assets.Scripts.Stuff;
using Assets.Scripts.Utils;
using System;
using UnityEngine;

namespace Assets.Scripts.Bases
{
    internal static class StaticBehaviour
    {
        private static Action<Label, Ksid, float, Vector3> applyDamageAction = ApplyDamage;

        public static void KillWithEffect(this Label label, Vector3 pos)
        {
            var settings = label.GetSettings();
            if (settings != null && settings.DeathEffect != null)
            {
                var pe = settings.DeathEffect.CreateWithotInit(label.LevelGroup, pos);
                pe.Init(5f);
            }
            label.Kill();
        }

        public static void Explode(this Label label)
        {
            Game.Instance.PrefabsStore.Explosion.Create(label.LevelGroup, label.transform.position, null);
            label.Kill();
        }

        public static void ApplyDamageDelayed(this Label label, Ksid damageType, float intensity, Vector3 hitPosition)
        {
            if (label.KsidGet.IsChildOf(damageType))
            {
                var hitOffset = hitPosition - label.transform.position;
                Game.Instance.GlobalTimerHandler.WithKsidFloatVectorParams.Plan(0.05f, applyDamageAction, label, damageType, intensity, hitOffset);
            }
        }

        /// <summary>
        /// Radeji volej z casovace, at se ti neprovedou kill efekty uvnitr nejakyho slozityho procesingu
        /// </summary>
        private static void ApplyDamage(this Label label, Ksid damageType, float intensity, Vector3 hitOffset)
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
                    var hitPosition = label.transform.position + hitOffset;
                    if (label.TryGetComponent<Status>(out var status))
                    {
                        status.ApplyDamage(damageType, intensity, hitPosition);
                    }
                    else
                    {
                        label.KillWithEffect(hitPosition);
                    }
                }
            }
        }


        public static void ApplyImpactDamage(float impactSpeedSqr, Label myLabel, Label otherLabel, bool isSpring, Vector3 hitPosition)
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

            myLabel.ApplyDamageDelayed(Ksid.DamagedByImpact, selfDmg, hitPosition);
            if (otherRB == null || isSpring)
                otherLabel.ApplyDamageDelayed(Ksid.DamagedByImpact, otherDmg, hitPosition);
        }


        public static void ApplyKnifeDamage(float impactSpeedSqr, Label myLabel, Label otherLabel, Vector3 hitPosition)
        {
            // myLabel je nuz -> damage na otherLabel (jen pokud other nema RB, jinak to vyresi other's callback)
            if (!otherLabel.HasRB)
                ApplyKnifeDamageOneWay(impactSpeedSqr, otherLabel, myLabel, hitPosition);
            ApplyKnifeDamageOneWay(impactSpeedSqr, myLabel, otherLabel, hitPosition);
        }

        public static void ApplyKnifeDamageOneWay(float impactSpeedSqr, Label targetLabel, Label knifeLabel, Vector3 hitPosition)
        {
            if (knifeLabel.KsidGet.IsChildOf(Ksid.DealsKnifeDamage)
                && knifeLabel.TryGetComponent(out IKnife knife) && knife.IsActive)
            {
                if (targetLabel.KsidGet.IsChildOf(Ksid.DamagedByKnife))
                {
                    float dmg = ComputeKnifeDmg(knife, impactSpeedSqr);
                    if (dmg > 0)
                        targetLabel.ApplyDamageDelayed(Ksid.DamagedByKnife, dmg, hitPosition);
                }

                float cutLimit = knife.GetJointCutStretchLimit();
                if (cutLimit > 0)
                {
                    Placeable target = targetLabel.PlaceableC;
                    if (target != null && target.TryFindClosestRbJoint(hitPosition, out RbJoint closest))
                    {
                        float stretchLimit = MathF.Min(closest.MyObj.SpLimits.StretchLimit, closest.OtherObj.SpLimits.StretchLimit);
                        if (stretchLimit < cutLimit)
                            closest.Disconnect();
                    }
                }
            }
        }

        private static float ComputeKnifeDmg(IKnife knife, float impactSpeedSqr)
        {
            return knife.GetDmg() * Mathf.Max(PhysicsConsts.KnifeDmgMinFactor,
                Mathf.Sqrt(impactSpeedSqr) / PhysicsConsts.KnifeDmgRefSpeed);
        }

        public static void ApplyContactDamage(Label dealerLabel, Label targetLabel, Vector3 hitPosition)
        {
            if (dealerLabel.KsidGet.IsChildOf(Ksid.DealsContactDamage)
                && targetLabel.KsidGet.IsChildOf(Ksid.DamagedByContact))
            {
                var settings = dealerLabel.GetSettings();
                if (settings != null && settings.ContactDmgPerFrame > 0)
                {
                    targetLabel.ApplyDamageDelayed(Ksid.DamagedByContact, settings.ContactDmgPerFrame, hitPosition);
                }
            }
        }

        public static void ApplyContactDamageBidirectional(Label labelA, Label labelB, Vector3 hitPosition)
        {
            ApplyContactDamage(labelA, labelB, hitPosition);
            ApplyContactDamage(labelB, labelA, hitPosition);
        }

        public static void TryActivateByThrow(this Label obj)
        {
            if (obj.KsidGet.IsChildOf(Ksid.ActivatesByThrow))
            {
                if (obj.KsidGet.IsChildOf(Ksid.MultiActivatesByThrow))
                {
                    var components = ListPool<ICanActivate>.Rent();
                    obj.GetComponents(components);
                    foreach (var c in components)
                        c.Activate();
                    components.Return();
                }
                else if (obj.TryGetComponent(out ICanActivate ao))
                {
                    ao.Activate();
                }
            }
        }

        public static void TryActivateInHand(this Label obj, Character3 character)
        {
            if (obj.KsidGet.IsChildOf(Ksid.ActivatesInHand))
            {
                if (obj.KsidGet.IsChildOf(Ksid.MultiActivatesInHand))
                {
                    var components = ListPool<IHoldActivate>.Rent();
                    obj.GetComponents(components);
                    foreach (var c in components)
                        c.Activate(character);
                    components.Return();
                }
                else if (obj.TryGetComponent(out IHoldActivate ao))
                {
                    ao.Activate(character);
                }
            }
        }
    }
}
