using Assets.Scripts.Core;
using Assets.Scripts.Core.Inventory;
using Assets.Scripts.Utils;
using System;
using UnityEngine;

namespace Assets.Scripts.Bases
{
    [RequireComponent(typeof(PlaceableSibling))]
    public class Status : MonoBehaviour, IHasAfterMapPlaced
    {
        private Placeable placeable;
        private float currentHealth;
        private float healthReductionCounter;
        public string Name { get; private set; }
        public EnergySettings EnergySettings { get; private set; }

        private float energy;
        public float Food { get; private set; }
        public float Waste { get; private set; }
        public float SleepEnergy { get; private set; }
        public float OverEated => Mathf.Max(0, Food + Mathf.Max(0, Waste - 50) - 100);

        public Sprite Icon => placeable.Settings.Icon;
        public float CurrentHealth => currentHealth;
        public float Energy => energy;
        public float EnergyAndHealth => Energy + CurrentHealth;
        public float MaxHealth => placeable.Settings.MaxHealth;

        public bool Hungry => Food <= 20;
        public bool NeedSleep => SleepEnergy <= 20;
        public bool NeedSleepCriticly => SleepEnergy <= 0 && Energy < 20;
        public bool CanWakeUp => SleepEnergy >= 10;
        public bool NeedShit => Waste >= 50;
        public bool IsOverEated => OverEated > 10;

        private const float foodToEnergy = 10;

        void Awake()
        {
            placeable = GetComponent<Placeable>();
            EnergySettings = placeable.Settings.EnergySettings;
        }
        public void AfterMapPlaced(Map.Map map, Placeable placeableSibling, bool goesFromInventory)
        {
            if (!goesFromInventory)
            {
                currentHealth = placeable.Settings.MaxHealth;
                healthReductionCounter = 0;
                energy = 100;
                Food = 100;
                Waste = 0;
                SleepEnergy = 150;
            }
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

        public void Metabolize(MetabolicState state, float deltaTime)
        {
            var settings = EnergySettings;
            if (settings == null)
                return;

            if ((state & MetabolicState.Healing) != 0)
                currentHealth = Mathf.Min(currentHealth + settings.HealRate * deltaTime, MaxHealth);

            if ((state & MetabolicState.GoodSleep) != 0)
                SleepEnergy = Mathf.Min(SleepEnergy + settings.GoodSleepRate * deltaTime, 200);
            else if ((state & MetabolicState.Sleep) != 0)
                SleepEnergy = Mathf.Max(SleepEnergy, Mathf.Min(SleepEnergy + settings.SleepRate * deltaTime, 100));
            else
                ConsumeEnergy(settings.IdleCost * deltaTime, settings.DieByLowEnergy);

            float energyMax = 100 - OverEated * 0.7f;
            float energyGain = Mathf.Min(energyMax - energy, settings.MetabolicSpeed * deltaTime) / foodToEnergy;
            energyGain = Mathf.Min(energyGain, SleepEnergy);
            energyGain = Mathf.Min(energyGain, Food);

            if (energyGain > 0)
            {
                energy += energyGain * foodToEnergy;
                Food -= energyGain;
                Waste += energyGain;
                SleepEnergy -= energyGain;
            }
        }

        public void Eat(float foodEnergy)
        {
            foodEnergy = Mathf.Max(0, Mathf.Min(foodEnergy, 100 - OverEated));
            Food += foodEnergy;
        }

        public float Shit()
        {
            float ret = Waste;
            Waste = 0;
            return ret;
        }

        public void ConsumeEnergy(float energyLoss, bool useHealth)
        {
            var eLoss = Mathf.Min(energy, energyLoss);
            energy -= eLoss;
            if (useHealth && energyLoss > eLoss)
            {
                InternalHit(energyLoss - eLoss);
            }
        }

        private void InternalHit(float hLoss)
        {
            healthReductionCounter += hLoss;
            if (currentHealth > hLoss && healthReductionCounter < 5)
            {
                ReduceHealth(hLoss);
            }
            else
            {
                if (healthReductionCounter >= 5)
                    healthReductionCounter -= 5;
                StaticBehaviour.Hit(placeable, Ksid.DamagedByFatigue, hLoss, placeable.Center3D);
            }
        }

        // Nasobic pohybu (max rychlosti, vyska skoku): pod EnergySettings.MoveDiscountEnergy linearne k 0.
        // Dole oriznuto na MinMoveFactor - AI rychlostmi deli.
        public float MoveFactor
        {
            get
            {
                var settings = EnergySettings;
                if (settings == null || settings.MoveDiscountEnergy <= 0)
                    return 1;
                return Mathf.Clamp(Energy / settings.MoveDiscountEnergy, 0.01f, 1);
            }
        }

        public bool CanDo(EnergyAction action) => EnergySettings == null || (EnergySettings.PayByHealth ? EnergyAndHealth : Energy) >= EnergySettings[action].MinEnergy;

        // multiplier: u prubeznych akci deltaTime (Cost je pak cena za sekundu).
        public void Pay(EnergyAction action, float multiplier = 1)
        {
            if (EnergySettings != null)
                ConsumeEnergy(EnergySettings[action].Cost * multiplier, EnergySettings.PayByHealth);
        }

        public bool TryDo(EnergyAction action, float multiplier = 1)
        {
            if (!CanDo(action))
                return false;
            Pay(action, multiplier);
            return true;
        }
    }

    [Flags]
    public enum MetabolicState
    {
        None,
        Sleep = 1,
        GoodSleep = 2,
        Healing = 4,
    }
}
