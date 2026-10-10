using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using System;
using UnityEngine;

namespace Assets.Scripts.Ai
{
    // Fixni sloty smyslu - index do SenseResult[] na MonsterBrain.
    // POZOR, pridavej jen na konec, jinak rozbijes serializaci !!!
    public enum SenseId
    {
        Loot,       // predmet, ktery chce prisera sebrat
        Target,     // cil, na ktery prisera utoci (hazi predmety)
    }

    // Druh vypoctu smyslu - centralni switch v MonsterBrain.Sense.
    // POZOR, pridavej jen na konec, jinak rozbijes serializaci !!!
    public enum SenseKind
    {
        None,
        NearestKsid,    // nejblizsi Placeable daneho Ksid v okruhu Radius (mapa dle SecondaryMap)
    }

    public static class SenseIds
    {
        public static readonly int Count = Enum.GetValues(typeof(SenseId)).Length;
    }

    // Parametry jednoho smyslu. Default hodnota pole (0/None/false) znamena "nezadano" - override
    // v AiSettings.SenseOverrides prepise jen nedefaultni pole (bool tedy jde jen zapnout).
    // Doplnkove parametry pridavej SEM stejnym stylem; vypocet, ktery je nepotrebuje, je ignoruje.
    [Serializable]
    public struct SenseConfig
    {
        [Tooltip("Který slot smyslu záznam plní (pravidla čtou smysl podle slotu).")]
        public SenseId Slot;
        [Tooltip("Druh výpočtu. NearestKsid = nejbližší objekt daného Ksid v okruhu Radius.")]
        public SenseKind Kind;
        [Tooltip("Co smysl hledá (včetně potomků v hierarchii Ksid).")]
        public Ksid Ksid;
        [Tooltip("Dosah [m] od středu příšery.")]
        public float Radius;
        [Tooltip("Kandidát musí být vidět - raycast na terén (vrstva Default). Zkouší max. 3 nejbližší kandidáty.")]
        public bool NeedsSight;
        [Tooltip("Přeskoč kandidáty u míst, která si příšera pamatuje jako nedosažitelná (MarkUnreachable).")]
        public bool SkipUnreachable;
        [Tooltip(">0 = tunelové vidění: nejdřív hledej jen v okně této velikosti [m] kolem posledního nálezu - levnější a příšera nepřeskakuje mezi cíli.")]
        public float TrackRadius;
        [Tooltip("NeedsSight raycastuje vodorovně v každé z rovině zvlášť (nejdřív v rovině kandidáta, pak ve druhé) místo jednoho šikmého paprsku mezi středy.")]
        public bool SightPerPlane;
        [Tooltip("None = hlavní mapa. Jinak hledá na sekundární mapě - jen mezi objekty na ni registrovanými (PlaceableSettings.SecondaryMapIndex), zato levně na velký dosah.")]
        public SecondaryMap SecondaryMap;

        // Kopie b prepsana nedefaultnimi poli o (override per-field).
        public SenseConfig(in SenseConfig b, in SenseConfig o)
        {
            this = b;
            Slot = o.Slot;
            if (o.Kind != SenseKind.None) Kind = o.Kind;
            if (o.Ksid != default) Ksid = o.Ksid;
            if (o.Radius != 0) Radius = o.Radius;
            if (o.NeedsSight) NeedsSight = true;
            if (o.SkipUnreachable) SkipUnreachable = true;
            if (o.TrackRadius != 0) TrackRadius = o.TrackRadius;
            if (o.SightPerPlane) SightPerPlane = true;
            if (o.SecondaryMap != SecondaryMap.None) SecondaryMap = o.SecondaryMap;
        }
    }

    // Vysledek smyslu v cache blackboardu. Zamerne BEZ Label: objekt muze byt mezitim poolnut
    // a ozit jako neco jineho, takze se pracuje jen s pozici (stara max. maxAge).
    public struct SenseResult
    {
        public const int InvalidStep = int.MinValue;

        public int Step;            // fixed krok (+ slowOffset) posledniho vypoctu -> freshness
        public bool Found;
        public Vector2 Position;    // stred nalezeneho objektu
        public float Value;         // vzdalenost / mnozstvi / skore - vyznam dle smyslu
        public int ZPlanes;         // bitmaska z rovin nalezeneho objektu (bit 0 = z 0, bit 1 = z 0.5)
    }
}
