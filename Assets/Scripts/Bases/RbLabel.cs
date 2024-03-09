using Assets.Scripts.Core;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Bases
{
    public sealed class RbLabel : Label, ILevelPlaceabe
    {
        private int connectionCounter;
        private bool isAlive;

        public override Placeable PlaceableC => (Placeable)SubLabel;
        public override Transform ParentForConnections => SubLabel.ParentForConnections;
        public override bool IsGroup => true;
        public override Label Prototype => Game.Instance.PrefabsStore.RbBase;
        public override Ksid KsidGet => SubLabel.KsidGet;
        public override Rigidbody Rigidbody => GetComponent<Rigidbody>();
        public override bool IsAlive => isAlive;
        public override float GetMass() => Rigidbody.mass;
        public override void DetachKilledChild(Label child)
        {
            child.transform.SetParent(LevelGroup, true);
            Kill();
        }
        public override void Cleanup()
        {
            // schvalne vynechavam clenup connectables, protoze je nemam
            Game.Instance.GlobalTimerHandler.ObjectDied(this);
            Rigidbody.Cleanup();
            isAlive = false;
        }

        void ILevelPlaceabe.Instantiate(Map.Map map, Transform parent, Vector3 pos)
        {
            var p = Instantiate(this, parent);
            p.PlaceableC.LevelPlaceAfterInstanciate(map, pos);
        }
        bool ILevelPlaceabe.SecondPhase => false;

        private Label SubLabel => transform.GetComponentInFirstChildren<Label>().ToRealNull();

        public void OnCollisionEnter(Collision collision)
        {
            var dest = transform.GetComponentInFirstChildren<IPhysicsEvents>();
            if (dest != null)
                dest.OnCollisionEnter(collision);
        }

        public override void Init(Map.Map map) => isAlive = true;

        public void Init(bool startMoving, bool stopMoving, bool incConnection)
        {
            if (startMoving)
            {
                Rigidbody.isKinematic = false;
                PlaceableC.RegisterMovingObjRecursivelly();
            }
            else if (stopMoving)
            {
                Rigidbody.isKinematic = true;
            }
            
            if (incConnection)
                ChengeConnectionCounter(1);
        }

        public void StopMoving()
        {
            PlaceableC.UnRegisterMovingObjRecursivelly();
            if (connectionCounter == 0)
            {
                DetachMe();
            }
            else
            {
                Rigidbody.isKinematic = true;
            }
        }

        public void ChengeConnectionCounter(int delta)
        {
            connectionCounter += delta;
            if (connectionCounter == 0 && Rigidbody.isKinematic == true)
                DetachMe();
        }

        private void DetachMe()
        {
            SubLabel.transform.SetParent(transform.parent, true);
            Kill();
        }
    }
}
