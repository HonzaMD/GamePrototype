using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.Scripts.Core;
using Assets.Scripts.Stuff;
using UnityEngine;

namespace Assets.Scripts.Bases
{
    public enum DensityMode
    {
        BoundingBox,
        Circle,
    }

    [Serializable]
    public struct DamageResistance
    {
        [Tooltip("Typ poškození (Ksid), ke kterému se vztahuje tato rezistence")]
        public Ksid DamageType;
        [Tooltip("Procentuální snížení poškození (0 = žádná rezistence, 1 = úplná imunita)")]
        [Range(0f, 1f)]
        public float Resistance;
        [Tooltip("Plochá hodnota odečtená od poškození po aplikaci rezistence")]
        public float Armor;
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
        [Header("Identity")]
        [Tooltip("Nastavte nenull, pokud se objekt ma poolovat")]
        public Label Prototype;
        [Tooltip("Pro zobrazeni v inventari")]
        public Sprite Icon;
        [Tooltip("Poradi v inventari")]
        public int IconOrder;

        [Header("Main Flags")]
        [Tooltip("IsGroup")]
        public bool HasSubPlaceables;
        [Tooltip("Je soucast vetsiho celku? a tudiz nejde zabit, samostatne pohybovat a pod")]
        public bool Unseparable;
        [Tooltip("Pro objekty ktere maji byt ovladany pomoci pripojovaneho RbLabel")]
        public bool AutoAtachRB;

        [Header("Collision")]
        [Tooltip("Pokud je > 0, umisti se navic i do sekundarni mapy")]
        public SecondaryMap SecondaryMapIndex;
        [Tooltip("Trigger. Pokud se ma trigger dat i do sekundarni mapy, da se tam exklusivne")]
        public bool IsTrigger;
        [Tooltip("Objekt pri RefreshCoordinates prepocitava BoundingBox (PosOffset, Size)")]
        public bool RecomputeBB;
        [Tooltip("Objekt zabira natoceny obdelnik v mape (OBB misto AABB)")]
        public bool IsOBB;
        [Tooltip("Pro vypocet BB. Ofset pri neutralni pozici.")]
        public Vector2 BBPosOffset;
        [Tooltip("Pro vypocet BB. Size pri neutralni pozici.")]
        public Vector2 BBSize = new(0.5f, 0.5f);
        [Tooltip("Pri kolizich s piskem: Pokud true pouzije se BoundingBox. Pokud false, pouzije se sampling z kollideru (pamalejsi)")]
        public bool UseSimpleBBCollisions;


        [Header("Physics")]
        [Tooltip("Jak je vec tezka v kg? Pokud je mass vyplnena u RB, ma prednost. Pokud 0, vypocita se Mass z objemuu a hustoty")]
        public float Mass;
        [Tooltip("Pouziti pro vypocet hmotnosti. Default: 1000 - hustota vody")]
        public float Density = 1000f; // hustota vody
        [Tooltip("Jak se pro vypocet hmotnosti vypocita objem?")]
        public DensityMode DensityMode;

        [Tooltip("Limity pro staticke spoje mezi objekty: Tah")]
        public float SpStretchLimit = 1000f;
        [Tooltip("Limity pro staticke spoje mezi objekty: Tlak")]
        public float SpCompressLimit = 1000f;
        [Tooltip("Limity pro staticke spoje mezi objekty: Ohyb")]
        public float SpMomentLimit = 1000f;

        [Header("Events")]
        public bool RecievesOnCollisionEnter;
        public bool RecievesOnCollisionStay;
        public bool HasMultiplePhysicsEvents;

        [Header("Damage")]
        [Tooltip("Bazove poskozeni nozem. 0 = neni knife damage")]
        public float KnifeDmg;
        [Tooltip("Maximalni stretchLimit jointu, ktery nuz dokaze preriznou. 0 = nereze jointy")]
        public float KnifeJointCutStretchLimit;
        [Tooltip("Poskozeni kontaktem za fyzikalni frame. 0 = zadny kontaktni damage")]
        public float ContactDmgPerFrame;

        [Header("Health")]
        [Tooltip("Maximální zdraví objektu. 0 = objekt nemá systém zdraví")]
        public float MaxHealth;
        [Tooltip("Rezistence a armor pro jednotlivé typy poškození")]
        public DamageResistance[] DamageResistances;

        [Header("VFX")]
        [Tooltip("Pro animaci akci, jako je pouziti objektu")]
        public AnimationCurve ActivityAnimation;
        [Tooltip("Particle effect prefab pri zasahu. Null = zadny efekt")]
        public ParticleEffect HitEffect;
        [Tooltip("Particle effect prefab pri smrti. Null = zadny efekt")]
        public ParticleEffect DeathEffect;
    }
}
