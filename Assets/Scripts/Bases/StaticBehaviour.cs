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
                    label.Kill();
                }
            }
        }
    }
}
