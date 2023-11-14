using Assets.Scripts.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.MaterialProperty;

namespace Assets.Scripts.Bases
{
    public class RbJoint : ConnectableLabel
    {
        [Flags]
        public enum State : byte
        {
            None = 0,
            RbConnection = 1,
            OwnsJoint = 2,
            SpConnection = 4,
        }


        public Joint Joint;
        public Placeable MyObj;
        public Placeable OtherObj;
        public RbJoint OtherConnectable;
        public State state;


        public override void Disconnect()
        {
            if (Joint && (state & State.OwnsJoint) != 0)
                Destroy(Joint);
            if ((state & State.SpConnection) != 0 && MyObj.SpNodeIndex != 0 && OtherObj.SpNodeIndex != 0)
                Game.Instance.StaticPhysics.RemoveJoint(MyObj.SpNodeIndex, OtherObj.SpNodeIndex);

            OtherConnectable.Kill();
            Kill();
        }

        internal void Setup(Placeable myObj, Placeable otherObj, RbJoint otherJ)
        {
            MyObj = myObj;
            OtherObj = otherObj;
            OtherConnectable = otherJ;
        }

        internal void SetupJoint(Joint j, bool ownsJoint)
        {
            SetupJoint1(ownsJoint);
            SetupJoint2(j);
        }


        internal void SetupJoint1(bool ownsJoint)
        {
            if (state != State.None)
                throw new InvalidOperationException("Necekal jsem pripojeny Joint");
            SetupJointMy(ownsJoint);
            OtherConnectable.SetupJointMy(ownsJoint);
        }

        internal void SetupJoint2(Joint j)
        { 
            Joint = j;
            OtherConnectable.Joint = j;
        }

        private void SetupJointMy(bool ownsJoint)
        {
            state = ownsJoint ? State.RbConnection | State.OwnsJoint : State.RbConnection;
            MyObj.AttachRigidBody(false, true);
        }

        private void Kill()
        {
            if ((state & State.RbConnection) != 0)
                MyObj.DetachRigidBody(false, true);

            state = State.None;
            Joint = null;
            MyObj = null;
            OtherObj = null;
            OtherConnectable = null;
            Game.Instance.ConnectablePool.Store(this, Game.Instance.PrefabsStore.RbJoint);
        }

        internal void SetupSp()
        {
            throw new NotImplementedException();
        }

        internal void SetupJoint()
        {
            SetupJoint1(true);
            var j = MyObj.Rigidbody.gameObject.AddComponent<FixedJoint>();
            var limits1 = MyObj.SpLimits;
            var limits2 = OtherObj.SpLimits;
            j.breakForce = MathF.Min(
                Math.Min(limits1.CompressLimit, limits2.CompressLimit),
                Math.Min(limits1.StretchLimit, limits2.StretchLimit)
                );
            j.breakTorque = Math.Min(limits1.MomentLimit, limits2.MomentLimit);
            j.connectedBody = OtherObj.Rigidbody;
            SetupJoint2(j);
        }

        internal void ClearSp()
        {
            state &= ~State.SpConnection;
            OtherConnectable.state &= ~State.SpConnection;
        }
    }
}
