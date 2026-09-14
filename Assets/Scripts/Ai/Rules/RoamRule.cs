using System;

namespace Assets.Scripts.Ai.Rules
{
    // Fallback na konec seznamu: kdyz nikdo vyssi nerozhodl, toulej se.
    [Serializable]
    public class RoamRule : IAiRule
    {
        public bool CanBeInterrupted => true;

        public bool Test(MonsterBrain brain) => true;

        public void Begin(MonsterBrain brain) => brain.SetDirective(DirectiveKind.Roam);

        public ActionStatus Tick(MonsterBrain brain) => ActionStatus.Done;
    }
}
