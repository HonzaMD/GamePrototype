using System;
using System.Reflection;
using UnityEngine;

namespace Assets.Scripts.Bases
{
    // Akce placene energii. Ke kazde hodnote patri stejnojmenny field EnergyCost v EnergySettings.
    // Poradi nic nenese - assety ukladaji ceny podle jmena fieldu.
    public enum EnergyAction
    {
        Jump,
        Catch,      // prubezna: cena za sekundu viseni na rukou bez opory nohou
        PickUp,
        Throw,
        KnifeStab,
        DirtBuild,
    }

    [Serializable]
    public struct EnergyCost
    {
        [Tooltip("Kolik energie akce spotrebuje")]
        public float Cost;
        [Tooltip("Pod touto hladinou energie se akce odmitne")]
        public float MinEnergy;
    }

    // Metabolismus a ceny akci. Sdilene pro druh (PlaceableSettings.EnergySettings), za behu jen ke cteni.
    // Vsichni ctou pres Status.EnergySettings a nekesuji si ho - pozdeji tam muze byt
    // custom kopie patrici jedne instanci.

    // Plynuti day night casu - vezmu podle Terrarie: 24 minut = 24 hernich hodin = 1 herni den
    // Pomery jsou typicky nasobene deltatimem v sekundach
    [CreateAssetMenu]
    public class EnergySettings : ScriptableObject
    {
        [Header("Metabolismus")]
        [Tooltip("Jak rychle se leci behem uzdravovani")]
        public float HealRate = 150f / (5 * 60);
        [Tooltip("Jak rychle ziskava energii ze spanku")]
        public float SleepRate = 100f / (5 * 60);
        [Tooltip("Jak rychle ziskava energii ze spanku za dobrych podminek")]
        public float GoodSleepRate = 200f / (5 * 60);
        [Tooltip("Rychlost metabolismu")]
        public float MetabolicSpeed = 2;
        [Tooltip("Zda zakladni metabolismus zacne cerpat ze zivota, pokud dojde energie")]
        public bool DieByLowEnergy = true;
        [Tooltip("Zda se za akce da platit take zdravim")]
        public bool PayByHealth = false;

        [Header("Pohyb")]
        [Tooltip("Kolik platis za sec kdyz nespis")]
        public float IdleCost = 1000f / (48 * 60);
        [Tooltip("Pod touto energii linearne klesaji max rychlosti a vyska skoku (pri 0 energie na 0)")]
        public float MoveDiscountEnergy = 40;
        public EnergyCost Jump = new() { Cost = 3f, MinEnergy = 10 };

        [Header("Ruce")]
        public EnergyCost Catch = new() { Cost = 1f, MinEnergy = 8 };
        public EnergyCost PickUp = new() { Cost = 0.5f, MinEnergy = 0 };
        public EnergyCost Throw = new() { Cost = 3f, MinEnergy = 5 };

        [Header("Predmety")]
        public EnergyCost KnifeStab = new() { Cost = 5f, MinEnergy = 10 };
        public EnergyCost DirtBuild = new() { Cost = 10f, MinEnergy = 15 };

        private static FieldInfo[] costFields;

        // Runtime pravda, fieldy vyse jsou jen autorska data. Indexovane (int)EnergyAction.
        [NonSerialized]
        private EnergyCost[] costs;

        public ref readonly EnergyCost this[EnergyAction action] => ref (costs ??= ReadCosts())[(int)action];

        private EnergyCost[] ReadCosts()
        {
            costFields ??= FindCostFields();
            var result = new EnergyCost[costFields.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = (EnergyCost)costFields[i].GetValue(this);
            return result;
        }

        private static FieldInfo[] FindCostFields()
        {
            var names = Enum.GetNames(typeof(EnergyAction));
            var fields = new FieldInfo[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                fields[i] = typeof(EnergySettings).GetField(names[i]);
                if (fields[i] == null || fields[i].FieldType != typeof(EnergyCost))
                    throw new InvalidOperationException($"EnergySettings nema EnergyCost field {names[i]}");
            }
            return fields;
        }

        private void OnValidate() => costs = null;
    }
}
