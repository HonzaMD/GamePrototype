using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Utils;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Scripts.Stuff
{
    [RequireComponent(typeof(PlaceableSibling))]
    public class JoinerGlue : MonoBehaviour, IHasCleanup, ICanActivate, IPhysicsEvents, IConnector, IHasRbJointCleanup
    {
        private const float MaxGraphSqrDistance = 1f;

        private bool isActive;
        private RbJoint conn1;
        private RbJoint conn2;

        public void Activate()
        {
            DisconnectAll();
            isActive = true;
        }

        void IPhysicsEvents.OnCollisionEnter(Collision collision, Label otherLabel)
        {
            if (!isActive)
                return;

            var otherObj = otherLabel.KillableLabel() as Placeable;
            if (!otherObj)
                return;

            if (conn1 == null)
            {
                AttachFixed(otherObj);
            }
            else
            {
                if (!conn1.IsConnected)
                {
                    conn1.Disconnect();
                    conn1 = null;
                    AttachFixed(otherObj);
                    return;
                }

                if (conn1.OtherObj.Ksid.IsChildOf(Ksid.SpFixed) && otherObj.Ksid.IsChildOf(Ksid.SpFixed))
                    return;

                if (conn1.OtherObj.IsConnectedWithinSqrDistance(otherObj, MaxGraphSqrDistance))
                    return;

                AttachHinge(otherObj);
            }
        }

        void IPhysicsEvents.OnCollisionStay(Collision collision, Label otherLabel)
        {
        }

        private void AttachFixed(Placeable otherObj)
        {
            var myObj = GetComponent<Placeable>();
            var rbj = myObj.CreateRbJoint(otherObj);
            if (rbj.state == RbJoint.State.None)
            {
                rbj.SetupRb();
                rbj.Joint.enablePreprocessing = false;
                rbj.EnableRbjCleanup();
                conn1 = rbj;
            }
        }

        private void AttachHinge(Placeable otherObj)
        {
            var myObj = GetComponent<Placeable>();
            var rbj = myObj.CreateRbJoint(otherObj);
            if (rbj.state == RbJoint.State.None)
            {
                //rbj.SetupRb();

                rbj.SetupRb1(true);
                var myRb = myObj.Rigidbody;
                var otherRb = otherObj.Rigidbody;
                var worldAnchor = myObj.Center3D;

                //Vector2 dir2D = otherObj.Center - myObj.Center;
                //float d = dir2D.magnitude;

                //var j = myRb.gameObject.AddComponent<HingeJoint>();
                //j.axis = new Vector3(0, 0, 1);
                //j.anchor = myRb.transform.InverseTransformPoint(worldAnchor);
                //j.autoConfigureConnectedAnchor = false;
                //j.connectedBody = otherRb;
                //if (d > 0.001f)
                //{
                //    var delta = (Vector3)(dir2D * (SeparationGap / d));
                //    worldAnchor -= delta;
                //}

                //j.connectedAnchor = otherRb.transform.InverseTransformPoint(worldAnchor);
                //var (bf, bt) = rbj.UnityLimits();
                //j.breakForce = bf;
                //j.breakTorque = float.PositiveInfinity;
                //j.enablePreprocessing = false;
                //j.useSpring = true;
                //j.spring = new JointSpring() { damper = 1, spring = 0, targetPosition = 0 };
                //rbj.SetupRb2(j);

                var j = myRb.gameObject.AddComponent<ConfigurableJoint>();
                j.configuredInWorldSpace = true;
                j.anchor = myRb.transform.InverseTransformPoint(worldAnchor);
                j.autoConfigureConnectedAnchor = true;
                j.connectedBody = otherRb;
                var (bf, _) = rbj.UnityLimits();
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
                rbj.SetupRb2(j);

                //var j = myRb.gameObject.AddComponent<SpringJoint>();
                //j.anchor = myRb.transform.InverseTransformPoint(worldAnchor);
                //j.autoConfigureConnectedAnchor = true;
                //j.connectedBody = otherRb;
                //var (bf, bt) = rbj.UnityLimits();
                //j.breakForce = bf;
                //j.breakTorque = float.PositiveInfinity;
                //j.spring = 10000;
                //j.damper = 50;
                //j.axis = Vector3.forward;
                //j.enablePreprocessing = false;
                //rbj.SetupRb2(j);

                rbj.EnableRbjCleanup();
                conn2 = rbj;
                isActive = false;
            }
        }

        void IConnector.Disconnect(Label label)
        {
            if (conn1 && conn1.OtherObj == label)
                conn1.Disconnect();
            if (conn2 && conn2.OtherObj == label)
                conn2.Disconnect();
        }

        void IHasRbJointCleanup.RbJointCleanup(RbJoint rbj)
        {
            if (conn1 == rbj) conn1 = null;
            if (conn2 == rbj) conn2 = null;
        }

        public void Cleanup(bool goesToInventory)
        {
            isActive = false;
            DisconnectAll();
        }

        private void DisconnectAll()
        {
            if (conn1) { conn1.Disconnect(); conn1 = null; }
            if (conn2) { conn2.Disconnect(); conn2 = null; }
        }
    }
}
