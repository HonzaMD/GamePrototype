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
        private static Action<Label, Ksid, float, Vector3> resolveHitAction = ResolveHit;

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

        /// <summary>
        /// Vstupní bod pro úder. Pokud label nemá vůbec mít damageType řešit, zahodí ho.
        /// Použij když volající dopředu netestuje kompatibilitu.
        /// </summary>
        public static void TryHit(this Label label, Ksid damageType, float intensity, Vector3 hitPosition)
        {
            if (label.KsidGet.IsChildOf(damageType))
                label.Hit(damageType, intensity, hitPosition);
        }

        /// <summary>
        /// Vstupní bod pro úder. Aplikuje rezistence/armor, spustí hit efekt a naplánuje
        /// vyhodnocení dopadu (HP/smrt) s krátkou prodlevou. Volat jen pokud volající
        /// už ověřil že label může damageType obdržet — jinak použij TryHit.
        /// </summary>
        public static void Hit(this Label label, Ksid damageType, float intensity, Vector3 hitPosition)
        {
            var settings = label.GetSettings();
            float effective = ComputeEffectiveDamage(settings, damageType, intensity);
            if (effective <= 0f)
                return;

            SpawnHitEffect(settings, label, hitPosition);
            var hitOffset = hitPosition - label.transform.position;
            Game.Instance.GlobalTimerHandler.WithKsidFloatVectorParams.Plan(0.05f, resolveHitAction, label, damageType, effective, hitOffset);
        }

        private static float ComputeEffectiveDamage(PlaceableSettings settings, Ksid damageType, float intensity)
        {
            if (settings == null)
                return intensity;

            var resistances = settings.DamageResistances;
            if (resistances == null)
                return intensity;

            float passthrough = 1f;
            float totalArmor = 0f;
            for (int i = 0; i < resistances.Length; i++)
            {
                ref var entry = ref resistances[i];
                if (damageType.IsChildOfOrEq(entry.DamageType))
                {
                    passthrough *= 1f - entry.Resistance;
                    totalArmor += entry.Armor;
                }
            }
            return Mathf.Max(0f, intensity * passthrough - totalArmor);
        }

        private static void SpawnHitEffect(PlaceableSettings settings, Label label, Vector3 hitPosition)
        {
            if (settings == null || settings.HitEffect == null)
                return;
            var pe = settings.HitEffect.CreateWithotInit(label.LevelGroup, hitPosition);
            pe.Init(2f);
        }

        /// <summary>
        /// Vyhodnocení dopadu po prodlevě. Status spravuje HP a rozhoduje o smrti,
        /// jinak zásah objekt rovnou killuje (případně přes death override).
        /// </summary>
        private static void ResolveHit(this Label label, Ksid damageType, float damage, Vector3 hitOffset)
        {
            if (label.TryGetComponent<Status>(out var status))
            {
                if (!status.ReduceHealth(damage))
                    return;
            }

            var hitPosition = label.transform.position + hitOffset;

            if (label.KsidGet.IsChildOf(Ksid.HasDeathOverride))
            {
                if (TryDieExploding(label, damageType)) 
                    return;
                if (TryDieShattering(label, damageType))
                    return;
                // future death overrides
            }
            label.KillWithEffect(hitPosition);
        }

        private static bool TryDieExploding(Label label, Ksid damageType)
        {
            if (!label.KsidGet.IsChildOf(Ksid.Explosive) || !damageType.IsChildOf(Ksid.CausesExplosion)) 
                return false;
            label.Explode();
            return true;
        }

        private static bool TryDieShattering(Label label, Ksid damageType)
        {
            if (!label.KsidGet.IsChildOf(Ksid.Dirt) || !damageType.IsChildOf(Ksid.CausesShatter))
                return false;
            label.Shatter();
            return true;
        }


        // Mřížka pro rozsypání hlíny na gravel (2 sloupce x 3 řady = 6 kousků na buňku)
        private const int GravelCols = 2;
        private const int GravelRows = 3;

        /// <summary>
        /// Rozbije hlínu. Pokud je hlína zarovnaná na buňku (má FullEx flag), vznikne na jejím
        /// místě SandCombiner, jinak se rozsype na mřížku gravel objektů. Původní hlina mohla
        /// zabírat obě z-vrstvy — pak se náhrada vytvoří pro obě, jinak jen pro tu kterou zabírá.
        /// </summary>
        private static void Shatter(this Label label)
        {
            Placeable dirt = label.PlaceableC;
            if (dirt == null)
                return;

            Map.Map map = dirt.GetMap();
            Transform parent = dirt.LevelGroup;
            bool aligned = dirt.CellBlocking.IsCellAligned();

            bool layer0 = dirt.CellBlocking.IsPartBlock0();
            bool layer1 = dirt.CellBlocking.IsPartBlock1();

            Vector2Int cell = map.WorldToCell(dirt.Center);
            Vector2 center = dirt.Center;
            // jen Z rotace (2.5D), ať natočení hlíny neovlivní z souřadnici gravelu
            Quaternion rot = Quaternion.Euler(0, 0, dirt.transform.eulerAngles.z);

            dirt.Kill();

            if (layer0)
                ShatterLayer(map, parent, aligned, 0, cell, center, rot);
            if (layer1)
                ShatterLayer(map, parent, aligned, 1, cell, center, rot);
        }

        private static void ShatterLayer(Map.Map map, Transform parent, bool aligned, int cellz, Vector2Int cell, Vector2 center, Quaternion rot)
        {
            if (aligned)
            {
                Vector3 pos = map.CellToWorld(cell).AddZ(cellz * 0.5f);
                var combiner = Game.Instance.PrefabsStore.SandCombinerFull.CreateWithotInit(parent, pos);
                combiner.InitFull(map);
            }
            else
            {
                SpawnGravelGrid(map, parent, cellz, center, rot);
            }
        }

        private static void SpawnGravelGrid(Map.Map map, Transform parent, int cellz, Vector2 center, Quaternion rot)
        {
            // v 50 % případů mřížku otočíme o dalších 90° -> střídá se 2x3 a 3x2
            if (UnityEngine.Random.value < 0.5f)
                rot *= Quaternion.Euler(0, 0, 90f);

            float z = cellz * 0.5f;
            Vector2 half = Map.Map.CellSize2d * 0.5f;
            float slotW = Map.Map.CellSize2d.x / GravelCols;
            float slotH = Map.Map.CellSize2d.y / GravelRows;
            // jitter jen v rámci slotu, ať se kousky nepřekrývají a pak se moc nerozletí
            float jitterX = slotW * 0.15f;
            float jitterY = slotH * 0.15f;

            for (int c = 0; c < GravelCols; c++)
            {
                for (int r = 0; r < GravelRows; r++)
                {
                    // offset od středu v lokálních osách hlíny, pak otočíme rotací hlíny
                    Vector2 local = new Vector2(
                        (c + 0.5f) * slotW - half.x + UnityEngine.Random.Range(-jitterX, jitterX),
                        (r + 0.5f) * slotH - half.y + UnityEngine.Random.Range(-jitterY, jitterY));
                    Vector3 pos = center.AddZ(z) + rot * (Vector3)local;
                    Game.Instance.PrefabsStore.Gravel.Create(parent, pos, map);
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

            myLabel.TryHit(Ksid.DamagedByImpact, selfDmg, hitPosition);
            if (otherRB == null || isSpring)
                otherLabel.TryHit(Ksid.DamagedByImpact, otherDmg, hitPosition);
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
                        targetLabel.Hit(Ksid.DamagedByKnife, dmg, hitPosition);
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
                    targetLabel.Hit(Ksid.DamagedByContact, settings.ContactDmgPerFrame, hitPosition);
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
