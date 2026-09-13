using Assets.Scripts.Bases;
using UnityEngine;

namespace Assets.Scripts.Ai
{
    // MOZEK prisery - mezicanek dedicneho retezce ChLegsArms -> MonsterBrain -> styl pohybu.
    // Framework je tu VZDY STEJNY; meni se jen rule set (MonsterSettings) a listova trida stylu.
    //
    // DVE NEZAVISLE SKLADACI OSY:
    //   styl pohybu - dedicnost (ktera komponenta je na prefabu); jedna volba na priseru,
    //                 chce tesny pristup k desired*/map/body i k blackboardu -> ma ho pres protected
    //   pravidla    - kompozice ([SerializeReference] v MonsterSettings); je jich mnoho, mix-and-match
    //
    // Vsechna AI bezi ve FIXED kroku spolu s motorem (50 Hz) - motor nesmi zaviset na frame rate
    // a rozhodovani z nej nema smysl odpojovat. Zadne deleni na "rychly gait" a "pomaly mozek";
    // drahe dotazy se rozhazuji v case pres SlowTick(period).
    //
    // Probuzena prisera je na per-frame listu (IActiveObject), spici na nem neni vubec - viz
    // ActivateAi/DeactivateAi (v kroku 6 to prevezme SleepController).
    public abstract class MonsterBrain : ChLegsArms, IActiveObject
    {
        public AiSettings AiSettings;

        public bool PendingRemove { get; set; }

        public Status Status { get; private set; }

        // --- Blackboard: action scratch ---------------------------------------------------
        // Protoze v jednu chvili bezi jen JEDNO pravidlo, staci jedna sdilena sada scratch poli.
        // Begin je inicializuje, Tick aktualizuje, pri Done se uvolni pro dalsi.
        private IAiRule running;
        private int runningIndex;
        protected float scratchTimer;
        protected int scratchPhase;
        protected Vector2 scratchPos;

        // TODO zivotnost cile: Label muze byt mezitim poolnut a ozit jako neco jineho, takze
        // ulozeny odkaz muze tise ukazovat na cizi objekt. Mame tri moznosti, jak to resit:
        //   a) Label vubec neukladat (jen pozici a pripadne Ksid, cil si pravidlo znovu najde),
        //   b) drzet cil pres Connectable (tracking uz existuje a odpojeni chodi callbackem),
        //   c) tagovaci mechanismus ala GlobalTimerHandler (Label + objTag, kontrola pri cteni).
        // Je to komplexnejsi vec, rozhodne se az bude prvni realny konzument (krok 4/5).
        protected Label scratchTarget;

        // --- Blackboard: motor command ---------------------------------------------------
        private Directive directive;
        public ref readonly Directive CurrentDirective => ref directive;

        // Rozhozeni drahych dotazu v case: kazda prisera dostane pri umisteni jiny ofset, takze
        // ~100 probuzenych priser nedela sken ve stejnem fixed kroku (jinak pravidelny spike).
        private int slowOffset;
        private static int nextSlowOffset;

        protected void AwakeM()
        {
            AwakeB();
            Status = GetComponent<Status>();
        }

        // Perioda je na pravidle (zna dulezitost informace): 25 = ~2x za sekundu pri 50 Hz.
        public bool SlowTick(int period) => (Game.Instance.FixedStepCounter + slowOffset) % period == 0;

        public void GameUpdate()
        {
        }

        public override void GameFixedUpdate()
        {
            Think();

            // Styl prelozi standing rozkaz na desired*. None = jeste nikdo nerozhodl,
            // Manual = pravidlo si desired* ridi samo - v obou pripadech styl nevolame.
            if (directive.Kind != DirectiveKind.None && directive.Kind != DirectiveKind.Manual)
                ApplyDirective(in directive);

            AdjustLegsArms(true);
            base.GameFixedUpdate();
        }

        private void Think()
        {
            var settings = AiSettings;
            if (settings == null)
                return;

            // 1) Modifiery - VZDY vsechny, shora dolu
            var modifiers = settings.Modifiers;
            for (int i = 0; i < modifiers.Length; i++)
            {
                if (modifiers[i].Test(this))
                    modifiers[i].Apply(this);
            }

            // 2) Pravidla - arbitraz pohyboveho zamku
            var rules = settings.Rules;
            if (running != null && !running.CanBeInterrupted)
            {
                TickRunning();
                return;
            }

            // bezici pravidlo smi prebit jen pravidlo NAD nim (nizsi priorita neprebiji)
            int limit = running == null ? rules.Length : runningIndex;
            for (int i = 0; i < limit; i++)
            {
                if (!rules[i].Test(this))
                    continue;

                rules[i].Begin(this);
                running = rules[i].Tick(this) == ActionStatus.Running ? rules[i] : null;
                runningIndex = i;
                return;                 // prvni, kdo zabere, vyhrava; pravidla se neretezi
            }

            if (running != null)        // nikdo vyssi nezabral -> pokracuj v beziciim
                TickRunning();
        }

        private void TickRunning()
        {
            if (running.Tick(this) == ActionStatus.Done)
                running = null;
        }

        // Prekladac rozkaz -> desired*. Overriduje ho STYL (listova trida) - ten jediny vi,
        // jak se telo v danem terenu pohybuje (lezec hlida diry a hrany, balon steeruje a odrazi se).
        protected virtual void ApplyDirective(in Directive d)
        {
        }

        public void SetDirective(DirectiveKind kind, float speedScale = 1f)
        {
            directive.Kind = kind;
            directive.SpeedScale = speedScale;
        }

        public void SetDirective(DirectiveKind kind, int direction, float speedScale = 1f)
        {
            directive.Kind = kind;
            directive.Direction = direction;
            directive.SpeedScale = speedScale;
        }

        public void SetDirective(DirectiveKind kind, Vector2 target, float speedScale = 1f)
        {
            directive.Kind = kind;
            directive.Target = target;
            directive.SpeedScale = speedScale;
        }

        public override void AfterMapPlaced(Map.Map map, Placeable placeableSibling, bool goesFromInventory)
        {
            base.AfterMapPlaced(map, placeableSibling, goesFromInventory);
            ResetBlackboard();
            slowOffset = nextSlowOffset++ & 0xFFFF;
            ActivateAi();
        }

        public override void Cleanup(bool goesToInventory)
        {
            DeactivateAi();
            base.Cleanup(goesToInventory);
        }

        // Reset kvuli poolingu - prefab se vraci s vyplnenym blackboardem po predchozim zivote.
        private void ResetBlackboard()
        {
            running = null;
            runningIndex = 0;
            scratchTimer = 0;
            scratchPhase = 0;
            scratchPos = default;
            scratchTarget = null;
            directive = default;        // Kind = None
        }

        // Jedine misto, ktere (de)registruje mozek do per-frame smycky. V kroku 6 to prevezme
        // SleepController - spici prisera nesmi byt na zadnem listu.
        protected void ActivateAi()
        {
            Game.Instance.ActivateObject(this);
            placeable.AlwaysMapMove = true;
        }

        protected void DeactivateAi()
        {
            Game.Instance.DeactivateObject(this);
            placeable.AlwaysMapMove = false;
        }
    }
}
