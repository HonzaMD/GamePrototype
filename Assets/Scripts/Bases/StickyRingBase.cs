using UnityEngine;

namespace Assets.Scripts.Bases
{
    /// <summary>
    /// Lepi SpringJointem v miste dotyku. Pri plnych slotech zeslabi posledni spojeni a prepise nejstarsi (kruhovy buffer).
    /// </summary>
    public abstract class StickyRingBase : StickyBase
    {
        [Header("Sticky joint")]
        public float BreakLimit = 600;      // breakForce i breakTorque
        public float WeakBreakLimit = 540;  // po zeslabeni, kdyz se lepi dalsi
        public float Spring = 1000;
        public float Damper = 5;

        private int lastSlot;

        protected override int SlotCount => 3;

        protected override void OnTouch(Placeable other, Collision collision)
        {
            if (collision.contactCount == 0)
                return;

            if (!IsFree(lastSlot))
            {
                Weaken(joints[lastSlot].Joint);
                lastSlot = (lastSlot + 1) % SlotCount; // TryAttach dalsi slot uvolni
            }

            if (!TryAttach(lastSlot, other, out SpringJoint j))
                return;

            var point = collision.GetContact(0).point;
            var awayDir = (point - transform.position).normalized * 0.1f;
            Vector3 p1 = point - awayDir;
            Vector3 p2 = point + awayDir;
            var otherRB = other.Rigidbody;

            j.autoConfigureConnectedAnchor = false;
            j.spring = Spring;
            j.damper = Damper;
            j.enableCollision = true;
            j.anchor = j.transform.InverseTransformPoint(p1);
            j.connectedBody = otherRB;
            j.connectedAnchor = otherRB.transform.InverseTransformPoint(p2);
            j.breakForce = BreakLimit;
            j.breakTorque = BreakLimit;
        }

        private void Weaken(Joint j)
        {
            if (!j) // SP spojeni nema Unity joint
                return;
            j.breakForce = WeakBreakLimit;
            j.breakTorque = WeakBreakLimit;
        }
    }
}
