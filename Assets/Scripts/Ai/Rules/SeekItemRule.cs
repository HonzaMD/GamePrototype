using System;
using UnityEngine;

namespace Assets.Scripts.Ai.Rules
{
    // Jdi k predmetu, ktery hlasi smysl (GoToward). Kdyz se k nemu dlouho nepriblizuje (zed, dira,
    // predmet na rimse...), prohlasi misto za nedosazitelne a skonci - nastoupi nizsi pravidlo (Roam).
    // Pokrok hlida MonsterBrain.TrackApproach (scratch). Kdyz uz ma dost (MaxCount), nehleda.
    // Saturace se meni jen sebranim (PickUpItemRule, ktere Seek prebije), staci ji testovat v Test.
    [Serializable]
    public class SeekItemRule : IAiRule
    {
        [Tooltip("Smysl, který hlásí hledaný předmět.")]
        public SenseId SenceId = SenseId.Loot;
        [Tooltip("Max. stáří smyslu v Test [s].")]
        public float SenceAge = 1.5f;
        [Tooltip("Max. stáří smyslu během cesty k předmětu [s].")]
        public float SenceTrackAge = 0.4f;
        [Tooltip("Násobek rychlosti při cestě k předmětu.")]
        public float SpeedScale = 1f;
        [Tooltip("Jak dlouho [s] smí být bez pokroku k cíli, než to vzdá.")]
        public float Patience = 1.5f;
        [Tooltip("Jak dlouho [s] si pamatuje místo jako nedosažitelné.")]
        public float GiveUpTime = 15f;
        [Tooltip("Saturace: s tolika kusy v inventáři už nehledá.")]
        public int MaxCount = 3;

        public bool CanBeInterrupted => true;

        public bool Test(MonsterBrain brain)
            => brain.Sense(SenceId, SenceAge).Found && !brain.IsSaturated(SenceId, MaxCount);

        public void Begin(MonsterBrain brain)
        {
            var target = brain.Sense(SenceId, SenceAge).Position;
            brain.StartApproach(target);
            brain.SetDirectiveAndLookAt(DirectiveKind.GoToward, target, SpeedScale);
        }

        public ActionStatus Tick(MonsterBrain brain)
        {
            ref readonly var s = ref brain.Sense(SenceId, SenceTrackAge);
            if (!s.Found)
            {
               // brain.MarkUnreachable(brain.ScratchPos, GiveUpTime);
                return ActionStatus.Done;
            }
            var target = s.Position;

            if (!brain.TrackApproach(target, Patience))
            {
                brain.MarkUnreachable(target, GiveUpTime);
                return ActionStatus.Done;
            }

            brain.SetDirectiveAndLookAt(DirectiveKind.GoToward, target, SpeedScale);   // cil se mohl pohnout
            return ActionStatus.Running;
        }
    }
}
