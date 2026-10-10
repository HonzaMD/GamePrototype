using Assets.Scripts.Core;
using System;
using UnityEngine;

namespace Assets.Scripts.Ai.Rules
{
    // Vidi-li prisera cil a ma v inventari predmet ItemKsid, vytahne ho do ruky, chvili miri a hodi.
    // Draha (vc. volby z roviny) se pocita uz v Test - jen kazdych SolveInterval, at test zustane
    // levny a pravidlo bezstavove; bez drahy se predmet ani nevytahuje. Rovinu z Testu si pamatuje
    // a predmet do ni prenda po uchopeni. Pred hodem se draha prepocita (cil se mohl pohnout);
    // kdyz uz to nejde, skonci bez hodu.
    // Preferenci predmetu urcuje poradi: ThrowItemRule(bomba) nad ThrowItemRule(kamen).
    //
    // Drzeni je prikaz na jeden krok (DrawItem) - kdyz pravidlo skonci bez hodu, predmet se vrati
    // do inventare sam.
    //
    // Scratch: ScratchPhase = faze (PhaseDraw/PhaseAim), ScratchTimer = start faze,
    // ScratchValue = z rovina hodu z Test.
    [Serializable]
    public class ThrowItemRule : IAiRule
    {
        private const int PhaseDraw = 0;
        private const int PhaseAim = 1;

        [Tooltip("Smysl, který hlásí cíl. Zásah do objektu s jeho Ksid se při testu dráhy bere jako volná cesta.")]
        public SenseId SenceId = SenseId.Target;
        [Tooltip("Max. stáří smyslu v Test [s].")]
        public float SenceAge = 0.5f;
        [Tooltip("Max. stáří smyslu během míření [s].")]
        public float SenceTrackAge = 0.2f;
        [Tooltip("Čím hází - první předmět v inventáři s tímto Ksid (včetně potomků).")]
        public Ksid ItemKsid = Ksid.Stone;
        [Tooltip("Povolené dráhy hodu a přesnost jejich testu.")]
        public ThrowStyle Style = new ThrowStyle { Direct = true, Lob = true, Segments = 4 };
        [Tooltip("Jak často [s] Test hledá dráhu hodu. Menší = příšera reaguje rychleji, ale víc raycastů.")]
        public float SolveInterval = 0.5f;
        [Tooltip("Čas [s] na uchopení vytaženého předmětu, pak to vzdá.")]
        public float DrawTimeout = 1f;
        [Tooltip("Pauza [s] mezi uchopením a hodem (míření).")]
        public float AimTime = 0.4f;

        // Hod je kratka jednorazova akce - dobehne.
        public bool CanBeInterrupted => false;

        public bool Test(MonsterBrain brain)
        {
            if (!brain.SlowTick(MonsterBrain.FixedSteps(SolveInterval)) || brain.CountItems(ItemKsid) == 0)
                return false;
            ref readonly var s = ref brain.Sense(SenceId, SenceAge);
            if (!s.Found || !brain.PlanThrow(in s, brain.SenseKsid(SenceId), ItemKsid, in Style, out var plan))
                return false;
            brain.ScratchValue = plan.CellZ;    // Begin nasleduje hned a ScratchValue nemaze
            return true;
        }

        public void Begin(MonsterBrain brain)
        {
            brain.ScratchPhase = PhaseDraw;
            brain.ScratchTimer = Time.time;
        }

        public ActionStatus Tick(MonsterBrain brain)
        {
            ref readonly var s = ref brain.Sense(SenceId, SenceTrackAge);
            if (!s.Found)
                return ActionStatus.Done;       // cil zmizel z dohledu

            brain.SetDirective(DirectiveKind.Stop);
            brain.LookAt(s.Position);
            if (!brain.DrawItem(ItemKsid))
                return ActionStatus.Done;

            if (brain.ScratchPhase == PhaseDraw)
            {
                if (brain.HoldsDrawnItem)
                {
                    // predmet je v ruce - rovinu prenda uz ted, at je hod citelny
                    brain.MoveHeldItemZ((int)brain.ScratchValue);
                    brain.ScratchPhase = PhaseAim;
                    brain.ScratchTimer = Time.time;
                }
                else if (Time.time - brain.ScratchTimer > DrawTimeout)
                {
                    return ActionStatus.Done;
                }
                return ActionStatus.Running;
            }

            if (Time.time - brain.ScratchTimer < AimTime)
                return ActionStatus.Running;

            if (brain.HoldsDrawnItem && brain.PlanThrow(in s, brain.SenseKsid(SenceId), ItemKsid, in Style, out var plan))
                brain.Throw(in plan);
            return ActionStatus.Done;
        }
    }
}
