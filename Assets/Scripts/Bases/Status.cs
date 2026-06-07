using Assets.Scripts.Core;
using Assets.Scripts.Core.Inventory;
using Assets.Scripts.Utils;
using UnityEngine;

namespace Assets.Scripts.Bases
{
    [RequireComponent(typeof(PlaceableSibling))]
    public class Status : MonoBehaviour, IHasAfterMapPlaced
    {
        private Placeable placeable;
        private float currentHealth;
        public string Name { get; private set; }

        public Sprite Icon => placeable.Settings.Icon;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => placeable.Settings.MaxHealth;

        void Awake()
        {
            placeable = GetComponent<Placeable>();
        }
        public void AfterMapPlaced(Map.Map map, Placeable placeableSibling, bool goesFromInventory)
        {
            currentHealth = placeable.Settings.MaxHealth;
        }

        public void SetupIdentity(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Odecte uz spocitany effective damage od HP. Vrati true pokud objekt ma byt zabit.
        /// Rezistence/armor a hit efekt resi StaticBehaviour.Hit pred zavolanim teto metody.
        /// </summary>
        public bool ReduceHealth(float damage)
        {
            currentHealth -= damage;
            return currentHealth <= 0f;
        }
    }
}
