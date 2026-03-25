using Assets.Scripts.Core;
using Assets.Scripts.Core.Inventory;
using UnityEngine;

namespace Assets.Scripts.Bases
{
    [RequireComponent(typeof(PlaceableSibling))]
    public class Status : MonoBehaviour
    {
        private Placeable placeable;
        private float currentHealth;
        public string Name { get; private set; }

        public Sprite Icon => placeable.Settings.Icon;
        public float CurrentHealth => currentHealth;

        void Awake()
        {
            placeable = GetComponent<Placeable>();
            currentHealth = placeable.Settings.MaxHealth;
        }

        public void SetupIdentity(string name)
        {
            Name = name;
        }

        public void ApplyDamage(Ksid damageType, float intensity)
        {
            var resistances = placeable.Settings.DamageResistances;
            float passthrough = 1f;
            float totalArmor = 0f;

            if (resistances != null)
            {
                for (int i = 0; i < resistances.Length; i++)
                {
                    ref var entry = ref resistances[i];
                    if (damageType.IsChildOfOrEq(entry.DamageType))
                    {
                        passthrough *= 1f - entry.Resistance;
                        totalArmor += entry.Armor;
                    }
                }
            }

            float damage = Mathf.Max(0f, intensity * passthrough - totalArmor);
            currentHealth -= damage;

            if (currentHealth <= 0f)
            {
                placeable.Kill();
            }
        }
    }
}
