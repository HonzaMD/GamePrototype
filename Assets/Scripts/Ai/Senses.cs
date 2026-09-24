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
    }

    // Druh vypoctu smyslu - centralni switch v MonsterBrain.Sense.
    // POZOR, pridavej jen na konec, jinak rozbijes serializaci !!!
    public enum SenseKind
    {
        None,
        NearestKsid,    // nejblizsi Placeable daneho Ksid v okruhu Radius (hlavni mapa)
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
        public SenseId Slot;
        public SenseKind Kind;
        public Ksid Ksid;
        public float Radius;
        public bool NeedsSight;         // kandidat musi byt videt (raycast na teren)
        public bool SkipUnreachable;    // preskoc mista, ktera si prisera pamatuje jako nedosazitelna
        public float TrackRadius;       // >0 = tunelove videni: nejdriv hledej v tomhle okne kolem posledniho nalezu
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
    }
}
