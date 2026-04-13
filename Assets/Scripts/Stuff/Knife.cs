using Assets.Scripts.Bases;
using Assets.Scripts.Utils;
using UnityEngine;

namespace Assets.Scripts.Stuff
{
    [RequireComponent(typeof(PlaceableSibling))]
    public class Knife : MonoBehaviour, IHoldActivate, ICanActivate, ISimpleTimerConsumer, IHasCleanup, IKnife
    {
        private const float ThrowActiveDuration = 3f;
        private static int defaultLayer = -1;

        private int activeTag;
        private int savedLayer;
        private GameObject colliderObj;
        int ISimpleTimerConsumer.ActiveTag { get => activeTag; set => activeTag = value; }
        public bool IsActive => (activeTag & 1) != 0;

        private void Awake()
        {
            colliderObj = GetComponentInChildren<Collider>().gameObject;
        }

        public float GetDmg() => GetComponent<PlaceableSibling>().Settings.KnifeDmg;
        public float GetJointCutStretchLimit() => GetComponent<PlaceableSibling>().Settings.KnifeJointCutStretchLimit;

        private void Deactivate()
        {
            if (IsActive)
            {
                activeTag++;
                colliderObj.layer = savedLayer;
            }
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
            if (defaultLayer < 0)
                defaultLayer = LayerMask.NameToLayer("Default");
            savedLayer = colliderObj.layer;
            colliderObj.layer = defaultLayer;
            this.Plan(duration);
        }

        void ISimpleTimerConsumer.OnTimer() => Deactivate();

        public void Cleanup(bool goesToInventory) => Deactivate();
    }
}
