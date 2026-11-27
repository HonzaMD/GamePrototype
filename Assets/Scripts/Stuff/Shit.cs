using Assets.Scripts.Bases;
using Assets.Scripts.Utils;

namespace Assets.Scripts.Stuff
{
    /// <summary>
    /// Lepi stejne jako StickyBomb, aktivuje se sama chvili po umisteni na mapu.
    /// </summary>
    public class Shit : StickyRingBase, IHasAfterMapPlaced, ISimpleTimerConsumer
    {
        private const float ActivationDelay = 0.7f;

        private int activeTag;
        private bool isActive;

        int ISimpleTimerConsumer.ActiveTag { get => activeTag; set => activeTag = value; }

        protected override bool IsSticky => isActive;

        void IHasAfterMapPlaced.AfterMapPlaced(Map.Map map, Placeable placeableSibling, bool goesFromInventory)
            => this.Plan(ActivationDelay);

        void ISimpleTimerConsumer.OnTimer() => isActive = true;

        protected override void OnCleanup(bool goesToInventory)
        {
            activeTag++; // zrusi naplanovanou aktivaci
            isActive = false;
        }
    }
}
