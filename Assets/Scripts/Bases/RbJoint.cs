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
            SetupJointMy(j, ownsJoint);
            OtherConnectable.SetupJointMy(j, ownsJoint);
        }

        private void SetupJointMy(Joint j, bool ownsJoint)
        {
            Joint = j;
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
    }
}
