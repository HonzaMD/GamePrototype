using System;

namespace Assets.Scripts.Ai.Rules
{
    // Jdi k predmetu, ktery hlasi smysl (GoToward). Kdyz se k nemu dlouho nepriblizuje (zed, dira,
    // predmet na rimse...), prohlasi misto za nedosazitelne a skonci - nastoupi nizsi pravidlo (Roam).
    // Pokrok hlida MonsterBrain.TrackApproach (scratch).
    [Serializable]
    public class SeekItemRule : IAiRule
    {
        public SenseId SenceId = SenseId.Loot;
        public float SenceAge = 1.5f;         // freshness smyslu [s]
        public float SenceTrackAge = 0.4f;         // freshness smyslu [s]
        public float SpeedScale = 1f;
        public float Patience = 1.5f;       // jak dlouho [s] smi byt bez pokroku, nez to vzda
        public float GiveUpTime = 15f;      // jak dlouho [s] si pamatuje misto jako nedosazitelne

        public bool CanBeInterrupted => true;

        public bool Test(MonsterBrain brain) => brain.Sense(SenceId, SenceAge).Found;

        public void Begin(MonsterBrain brain)
        {
            var target = brain.Sense(SenceId, SenceAge).Position;
            brain.StartApproach(target);
            brain.SetDirective(DirectiveKind.GoToward, target, SpeedScale);
        }

        public ActionStatus Tick(MonsterBrain brain)
        {
            ref readonly var s = ref brain.Sense(SenceId, SenceTrackAge);
            if (!s.Found)
                return ActionStatus.Done;
            var target = s.Position;

            if (!brain.TrackApproach(target, Patience))
            {
                brain.MarkUnreachable(target, GiveUpTime);
                return ActionStatus.Done;
            }

            brain.SetDirective(DirectiveKind.GoToward, target, SpeedScale);   // cil se mohl pohnout
            return ActionStatus.Running;
        }
    }
}
