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
        public Joint Joint;
        public Placeable MyObj;
        public Placeable OtherObj;
        public RbJoint OtherConnectable;


        public override void Disconnect()
        {
            if (Joint)
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

        private void Kill()
        {
            Joint = null;
            MyObj = null;
            OtherObj = null;
            OtherConnectable = null;
            Game.Instance.ConnectablePool.Store(this, Game.Instance.PrefabsStore.RbJoint);
        }
    }
}
