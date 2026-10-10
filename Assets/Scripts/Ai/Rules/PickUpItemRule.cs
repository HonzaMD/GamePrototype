using System;
using UnityEngine;

namespace Assets.Scripts.Ai.Rules
{
    // U predmetu ho sebere do inventare. Patri NAD SeekItemRule.
    // Test schvalne neresi dosah ruky - jinak by prisera pod predmetem na rimse stala navzdy
    // (Seek by ji drzel na miste). Vsechny pripady "dosel, ale nesebral" (rimsa, moc tezky
    // predmet) skonci timeoutem a mistem v pameti nedosazitelnych mist.
    //
    // Scratch: ScratchPhase = pocet kusu v inventari pri startu (vic = sebrano), ScratchTimer = start.
    [Serializable]
    public class PickUpItemRule : IAiRule
    {
        [Tooltip("Smysl, který hlásí předmět ke sebrání (jeho Ksid určuje, co se sbírá).")]
        public SenseId SenceId = SenseId.Loot;
        [Tooltip("Max. stáří smyslu v Test [s].")]
        public float SenceAge = 1.5f;
        [Tooltip("Max. stáří smyslu během sbírání [s].")]
        public float SenceTrackAge = 0.4f;
        [Tooltip("Saturace: s tolika kusy v inventáři už nesbírá.")]
        public int MaxCount = 3;
        [Tooltip("Vzdálenost [m] od předmětu, od které zkouší sebrat.")]
        public float Reach = 0.4f;
        [Tooltip("Čas [s] na sebrání, pak to vzdá a označí místo jako nedosažitelné.")]
        public float Timeout = 2f;
        [Tooltip("Jak dlouho [s] si pamatuje místo jako nedosažitelné.")]
        public float GiveUpTime = 15f;
        [Tooltip("Násobek rychlosti při přibližování během sbírání.")]
        public float SpeedScale = 0.5f;

        // Sbirani je prikaz na jeden krok (MonsterBrain.PickUp) - preruseni nic nenecha viset.
        // Ruka, ktera uz predmet tahne, ho motor dotahne nebo upusti sam.
        public bool CanBeInterrupted => true;

        public bool Test(MonsterBrain brain)
        {
            ref readonly var s = ref brain.Sense(SenceId, SenceAge);
            return s.Found
                && (s.Position - brain.Center).sqrMagnitude < Reach * Reach
                && !brain.IsSaturated(SenceId, MaxCount);
        }

        public void Begin(MonsterBrain brain)
        {
            brain.ScratchPhase = brain.CountItems(brain.SenseKsid(SenceId));
            brain.ScratchTimer = Time.time;
            brain.ScratchPos = brain.Sense(SenceId, SenceAge).Position;
        }

        public ActionStatus Tick(MonsterBrain brain)
        {
            var ksid = brain.SenseKsid(SenceId);
            if (brain.CountItems(ksid) > brain.ScratchPhase)
                return ActionStatus.Done;       // sebrano

            ref readonly var s = ref brain.Sense(SenceId, SenceTrackAge);
            if (!s.Found)
            {
                brain.MarkUnreachable(brain.ScratchPos, GiveUpTime);
                return ActionStatus.Done;       // predmet zmizel (sebral ho nekdo jiny)
            }
            
            var target = s.Position;
            brain.ScratchPos = target;

            if ((target - brain.Center).sqrMagnitude > Reach * Reach)
                return ActionStatus.Done;

            if (Time.time - brain.ScratchTimer > Timeout)
            {
                brain.MarkUnreachable(target, GiveUpTime);
                return ActionStatus.Done;
            }

            brain.SetDirectiveAndLookAt(DirectiveKind.GoToward, target, SpeedScale);
            brain.PickUp(ksid);
            return ActionStatus.Running;
        }
    }
}
