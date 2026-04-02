using Assets.Scripts.Bases;
using Assets.Scripts.Utils;
using UnityEngine;

namespace Assets.Scripts.Stuff
{
    [RequireComponent(typeof(PlaceableSibling))]
    public class Knife : MonoBehaviour, IHoldActivate, ICanActivate, ISimpleTimerConsumer, IHasCleanup, IKnife
    {
        private const float ThrowActiveDuration = 3f;

        private int activeTag;
        int ISimpleTimerConsumer.ActiveTag { get => activeTag; set => activeTag = value; }
        public bool IsActive => (activeTag & 1) != 0;

        public float GetDmg() => GetComponent<PlaceableSibling>().Settings.KnifeDmg;

        private void Deactivate()
        {
            if (IsActive)
                activeTag++;
            }

        public void Activate(Character3 character)
        {
            var settings = GetComponent<PlaceableSibling>().Settings;
            character.ActivateHoldAnimation(settings.ActivityAnimation, 0.55f, 2f);
            SetActive(0.55f);
        }

        void ICanActivate.Activate()
        {
            SetActive(ThrowActiveDuration);
        }

        private void SetActive(float duration)
        {
            Deactivate();
            this.Plan(duration);
        }

        void ISimpleTimerConsumer.OnTimer() => Deactivate();

        public void Cleanup(bool goesToInventory) => Deactivate();
    }
}
