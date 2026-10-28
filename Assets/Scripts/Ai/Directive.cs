using UnityEngine;

namespace Assets.Scripts.Ai
{
    public enum DirectiveKind
    {
        None,          // zadny rozkaz - vychozi stav po spawnu i po poolingu; styl se nevola
        Manual,        // pravidlo si ridi desired* samo; styl se nevola
        Stop,          // stuj
        Roam,          // toulej se (smer si vybira styl)
        GoDirection,   // jdi smerem Direction
        GoToward,      // jdi k Target
    }

    // STANDING prikaz pohybu v blackboardu. Nekoluje AI (zadne pravidlo necte cizi Kind) - je to
    // rozkaz, ktery styl (listova trida MonsterBrain) kazdy fixed krok preklada na desired*.
    // Diky tomu, ze plati, dokud ho nekdo nezmeni, jede prisera dal i ve framu, kdy zadne
    // pravidlo nerozhodne - pohyb se netrha a Tick pravidla muze byt skoro prazdny.
    public struct Directive
    {
        public DirectiveKind Kind;
        // 2D smer (GoDirection). Nemusi byt normalizovany - styly berou jen smer: letec se k nemu staci,
        // lezec ho promitne do 1D (znamenko x; x == 0 = stoji).
        public Vector2 Direction;
        public Vector2 Target;     // cil (GoToward)
        public float SpeedScale;   // 0..1 - zrychli (strach) / zpomali (unava)
    }
}
