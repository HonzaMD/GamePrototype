using UnityEngine;

namespace Assets.Scripts.Bases
{
    /// <summary>
    /// Zaklad pro objekty, ktere se pri srazce prilepi RbJointem. Drzi sloty a obsluhuje vsechny eventy odpojeni.
    /// Potomek urcuje pocet slotu, kdy lepi (IsSticky) a co pri dotyku vytvori (OnTouch + TryAttach).
    /// </summary>
    [RequireComponent(typeof(PlaceableSibling))]
    public abstract class StickyBase : MonoBehaviour, IHasCleanup, IPhysicsEvents, IConnector, IHasRbJointCleanup
    {
        protected RbJoint[] joints;
        protected Placeable myObj;

        protected abstract int SlotCount { get; }
        protected abstract bool IsSticky { get; }
        protected abstract void OnTouch(Placeable other, Collision collision);
        protected virtual void OnCleanup(bool goesToInventory) { }

        protected virtual void Awake()
        {
            myObj = GetComponent<Placeable>();
            joints = new RbJoint[SlotCount];
        }

        void IPhysicsEvents.OnCollisionEnter(Collision collision, Label otherLabel)
        {
            if (!IsSticky)
                return;
            var other = otherLabel.KillableLabel() as Placeable;
            if (!other || other == myObj || IsAttachedTo(other))
                return;
            OnTouch(other, collision);
        }

        void IPhysicsEvents.OnCollisionStay(Collision collision, Label otherLabel) { }

        void IConnector.Disconnect(Label label)
        {
            for (int i = 0; i < joints.Length; i++)
            {
                if (joints[i] && joints[i].OtherObj == label)
                    DisconnectSlot(i);
            }
        }

        void IHasRbJointCleanup.RbJointCleanup(RbJoint rbj)
        {
            for (int i = 0; i < joints.Length; i++)
            {
                if (joints[i] == rbj)
                    joints[i] = null;
            }
        }

        void IHasCleanup.Cleanup(bool goesToInventory)
        {
            DisconnectAll();
            OnCleanup(goesToInventory);
        }

        /// <summary>
        /// Uvolni slot, vytvori RbJoint a Unity joint T na GO s rigidbody (muze byt RbLabel).
        /// Potomek pak nastavi connectedBody, anchory (pres joint.transform) a limity.
        /// </summary>
        protected bool TryAttach<T>(int slot, Placeable other, out T joint) where T : Joint
        {
            DisconnectSlot(slot); // prasknuty joint ke stejnemu objektu by jinak CreateRbJoint vratil znovu
            var rbj = myObj.CreateRbJoint(other);
            if (rbj.state != RbJoint.State.None)
            {
                joint = null;
                return false;
            }
            rbj.SetupRb1(); // muze vytvorit RbLabel, proto Rigidbody az potom
            joint = myObj.Rigidbody.gameObject.AddComponent<T>();
            rbj.SetupRb2(joint);
            rbj.EnableRbjCleanup(this);
            joints[slot] = rbj;
            return true;
        }

        /// <summary>Slot je prazdny, nebo joint praskl a jeste ho neuklidil DisconnectBrokenRbJoints.</summary>
        protected bool IsFree(int slot) => !joints[slot] || !joints[slot].IsConnected;

        protected bool IsAttachedTo(Placeable p)
        {
            foreach (var j in joints)
            {
                if (j && j.OtherObj == p && j.IsConnected)
                    return true;
            }
            return false;
        }

        protected void DisconnectSlot(int slot)
        {
            if (joints[slot])
                joints[slot].Disconnect(); // -> RbJointCleanup vynuluje slot
        }

        protected void DisconnectAll()
        {
            for (int i = 0; i < joints.Length; i++)
                DisconnectSlot(i);
        }
    }
}
