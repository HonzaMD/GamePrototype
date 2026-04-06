using Assets.Scripts.Core;
using Assets.Scripts.Core.Inventory;
using Assets.Scripts.Utils;
using UnityEngine;

namespace Assets.Scripts.Bases
{
    [RequireComponent(typeof(PlaceableSibling))]
    public class KillAfterThrow : MonoBehaviour, ICanActivate, ISimpleTimerConsumer, IHasCleanup
    {
        private Placeable placeable;
        public int ActiveTag { get; set; }

        public void Activate()
        {
            this.Plan(3);
        }

        public void Cleanup(bool goesToInventory)
        {
            ActiveTag++;
        }

        public void OnTimer()
        {
            placeable.KillWithEffect(placeable.Center3D);
        }

        void Awake()
        {
            placeable = GetComponent<Placeable>();
        }

    }
}
