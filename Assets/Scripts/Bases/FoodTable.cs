using System;
using Assets.Scripts.Utils;
using UnityEngine;

namespace Assets.Scripts.Bases
{
    [Serializable]
    public struct FoodValue
    {
        public float Energy;
        public float Waste;
    }

    // Vyzivove hodnoty: radky = jidlo, sloupce = konzument.
    [CreateAssetMenu]
    public class FoodTable : KsidTable<FoodValue>
    {
        public override string RowsLabel => "Jídlo";
        public override string ColumnsLabel => "Konzument";
    }
}
