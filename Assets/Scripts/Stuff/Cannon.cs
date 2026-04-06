using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Utils;
using System;
using UnityEngine;

namespace Assets.Scripts.Stuff
{
    [RequireComponent(typeof(PlaceableSibling))]
    public class Cannon : MonoBehaviour, ISimpleTimerConsumer, IHasCleanup, IHasAfterMapPlaced
    {
        public Trigger ChildTrigger;
        public CannonSettings CannonSettings;
        private Placeable placeable;

        private ActivityTag4 activeTag; // 0 neaktivni, 1 cekam na triger, 2 cekam na delay z planovace (a neprijimam triger)
        private int shotsRemaining;

        int ISimpleTimerConsumer.ActiveTag { get => activeTag.Tag; set => activeTag.Tag = value; }

        public void Cleanup(bool goesToInventory)
        {
            activeTag.Reset();
        }

        private void Awake()
        {
            placeable = GetComponent<Placeable>();
            ChildTrigger.TriggerOnEvent += ChildTrigger_TriggerOnEvent;
        }

        private void ChildTrigger_TriggerOnEvent(Trigger obj)
        {
            if (activeTag.State == 1)
                TestShot();
        }

        private void TestShot()
        {
            if (ChildTrigger.ActiveObjects.Count == 0 || shotsRemaining <= 0)
                return;

            shotsRemaining--;
            var levelGroup = ChildTrigger.LevelGroup2;
            var obj = CannonSettings.Obj.Create(levelGroup.transform, ChildTrigger.transform.position, levelGroup.Map);
            if (obj.KsidGet.IsChildOf(Ksid.ActivatesByThrow) && obj.TryGetComponent(out ICanActivate ao))
                ao.Activate();
            var velocity = (ChildTrigger.transform.position - transform.position).normalized * CannonSettings.Speed + placeable.Velocity;
            obj.Rigidbody.linearVelocity = velocity;

            this.Plan(3);
        }

        public void OnTimer()
        {
            activeTag.SetState(1);
            TestShot();
        }

        public void AfterMapPlaced(Map.Map map, Placeable placeableSibling, bool goesFromInventory)
        {
            activeTag.Increment();
            shotsRemaining = CannonSettings.Shots;
        }
    }
}
