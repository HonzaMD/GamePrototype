using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Utils;
using UnityEngine;

namespace Assets.Scripts.Stuff
{
    public class JoinerGlue : StickyBase, ICanActivate
    {
        private const float MaxGraphSqrDistance = 1f;
        private const int FixedSlot = 0;
        private const int HingeSlot = 1;

        private bool isActive;

        protected override int SlotCount => 2;
        protected override bool IsSticky => isActive;

        public void Activate()
        {
            DisconnectAll();
            isActive = true;
        }

        protected override void OnCleanup(bool goesToInventory) => isActive = false;

        protected override void OnTouch(Placeable other, Collision collision)
        {
            if (IsFree(FixedSlot))
            {
                AttachFixed(other);
                return;
            }

            var fixedObj = joints[FixedSlot].OtherObj;
            if (fixedObj.Ksid.IsChildOf(Ksid.SpFixed) && other.Ksid.IsChildOf(Ksid.SpFixed))
                return;
            if (fixedObj.IsConnectedWithinSqrDistance(other, MaxGraphSqrDistance))
                return;

            AttachHinge(other);
        }

        private void AttachFixed(Placeable other)
        {
            if (!TryAttach(FixedSlot, other, out FixedJoint j))
                return;
            var (bf, bt) = joints[FixedSlot].UnityLimits();
            j.breakForce = bf;
            j.breakTorque = bt;
            j.connectedBody = other.Rigidbody;
            j.enablePreprocessing = false;
        }

        private void AttachHinge(Placeable other)
        {
            if (!TryAttach(HingeSlot, other, out ConfigurableJoint j))
                return;
            var (bf, _) = joints[HingeSlot].UnityLimits();
            j.configuredInWorldSpace = true;
            j.anchor = j.transform.InverseTransformPoint(myObj.Center3D);
            j.autoConfigureConnectedAnchor = true;
            j.connectedBody = other.Rigidbody; // az po anchor, kvuli autoConfigure
            j.breakForce = bf;
            j.breakTorque = float.PositiveInfinity;
            j.xMotion = ConfigurableJointMotion.Limited;
            j.yMotion = ConfigurableJointMotion.Limited;
            j.zMotion = ConfigurableJointMotion.Locked;
            j.angularXMotion = ConfigurableJointMotion.Locked;
            j.angularYMotion = ConfigurableJointMotion.Locked;
            j.angularZMotion = ConfigurableJointMotion.Free;
            j.linearLimit = new SoftJointLimit() { limit = 0.00f };
            j.linearLimitSpring = new SoftJointLimitSpring() { spring = 10000, damper = 8 };
            j.angularYZLimitSpring = new SoftJointLimitSpring() { damper = 0.5f };
            j.enablePreprocessing = false;
            isActive = false;
        }
    }
}
