using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Bases
{
    public enum DensityMode
    {
        BoundingBox,
        Circle,
    }

    public enum SecondaryMap
    {
        None,
        Beasts,
        Navigation,
        Last,
    }

    [CreateAssetMenu]
    public class PlaceableSettings : ScriptableObject
    {
        [Tooltip("IsGroup")]
        public bool HasSubPlaceables;
        [Tooltip("Pri kolizich s piskem: Pokud true pouzije se BoundingBox. Pokud false, pouzije se sampling z kollideru (pamalejsi)")]
        public bool UseSimpleBBCollisions;
        [Tooltip("Jak je vec tezka v kg? Pokud je mass vyplnena u RB, ma prednost. Pokud 0, vypocita se Mass z objemuu a hustoty")]
        public float Mass;
        [Tooltip("Pouziti pro vypocet hmotnosti. Default: 1000 - hustota vody")]
        public float Density = 1000f; // hustota vody
        [Tooltip("Jak se pro vypocet hmotnosti vypocita objem?")]
        public DensityMode DensityMode;
        [Tooltip("Nastavte nenull, pokud se objekt ma poolovat")]
        public Label Prototype;
        [Tooltip("Je soucast vetsiho celku? a tudiz nejde zabit, samostatne pohybovat a pod")]
        public bool Unseparable;
        [Tooltip("Pro objekty ktere maji byt ovladany pomoci pripojovaneho RbLabel")]
        public bool AutoAtachRB;
        [Tooltip("Trigger. Pokud se ma tragger dat i do sekundarni mapy, da se tam exklusivne")]
        public bool IsTrigger;
        [Tooltip("Pokud je > 0, umisti se navic i do sekundarni mapy")]
        public SecondaryMap SecondaryMapIndex;
        [Tooltip("Pro zobrazeni v inventari")]
        public Sprite Icon;
        [Tooltip("Poradi v inventari")]
        public int IconOrder;

        [Tooltip("Limity pro staticke spoje mezi objekty: Tah")]
        public float SpStretchLimit = 1000f;
        [Tooltip("Limity pro staticke spoje mezi objekty: Tlak")]
        public float SpCompressLimit = 1000f;
        [Tooltip("Limity pro staticke spoje mezi objekty: Ohyb")]
        public float SpMomentLimit = 1000f;
    }
}
