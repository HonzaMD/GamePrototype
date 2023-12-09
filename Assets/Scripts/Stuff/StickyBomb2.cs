using Assets.Scripts.Bases;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Assets.Scripts.Stuff
{
    [RequireComponent(typeof(PlaceableSibling), typeof(Rigidbody))]
    public class StickyBomb2 : MonoBehaviour, IHasCleanup, ICanActivate, ISimpleTimerConsumer, IConnector, IHasRbJointCleanup
    {
        private const float BreakForce = 600;
        private const float BreakTorque = 600;
        private const float WBreakForce = 540;
        private const float WBreakTorque = 540;


        private int activeTag;
        public Renderer Renderer;
        private ConnDesc[] connections = new ConnDesc[3];
        private int lastConnection;

        private struct ConnDesc
        {
            public RbJoint MyRbJ;
            public SpringJoint Joint;
            public bool Broken => !Joint;
            public bool Connected => MyRbJ;
            public Placeable OtherObj => MyRbJ ? MyRbJ.OtherObj : null;

            internal void Disconnect()
            {
                if (MyRbJ)
                    MyRbJ.Disconnect();
            }
        }

        int ISimpleTimerConsumer.ActiveTag { get => activeTag; set => activeTag = value; }
        private bool IsActive => (activeTag & 1) != 0;


        private static readonly int activeId = Shader.PropertyToID("_Active");
        private static readonly int time0Id = Shader.PropertyToID("_Time0");
        private static readonly int time1Id = Shader.PropertyToID("_Time1");
        private static MaterialPropertyBlock sharedPropertyBlock;

        void ISimpleTimerConsumer.OnTimer()
        {
            GetComponent<Label>().Explode();
        }

        public void Activate()
        {
            if (!IsActive)
            {
                SetShader(1, Time.time, Time.time + 10);
                this.Plan(10);
            }
        }

        private void SetShader(float active, float time0, float time1)
        {
            if (sharedPropertyBlock == null)
                sharedPropertyBlock = new MaterialPropertyBlock();
            sharedPropertyBlock.SetFloat(activeId, active);
            sharedPropertyBlock.SetFloat(time0Id, time0);
            sharedPropertyBlock.SetFloat(time1Id, time1);
            Renderer.SetPropertyBlock(sharedPropertyBlock);

            //var material = Renderer.material;
            //material.SetFloat("_Active", active);
            //material.SetFloat(time0Id, time0);
            //material.SetFloat(time1Id, time1);
            //HDMaterial.ValidateMaterial(material);
        }

        public void Cleanup()
        {
            Deactivate();
            GetComponent<Rigidbody>().Cleanup();
        }

        //private void Start()
        //{
        //    SetShader(1, 0, 1);
        //}

        private void Deactivate()
        {
            if (IsActive)
            {
                activeTag++;
                SetShader(0, 0, 1);
            }
            DisconnectJoints();
        }

        public void OnCollisionEnter(Collision collision)
        {
            if (IsActive && Label.TryFind(collision.collider.transform, out var label))
            {
                var otherObj = label.KillableLabel() as Placeable;
                if (IsAlreadyAttached(otherObj))
                    return;
                int index = FindNextConnectable();
                if (index >= 0)
                    AttachJoint(index, otherObj, collision.contacts[0].point);
            }
        }

        private bool IsAlreadyAttached(Placeable otherObj)
        {
            for (int f = 0; f < connections.Length; f++)
            {
                if (connections[f].OtherObj == otherObj && !connections[f].Broken)
                    return true;
            }
            return false;
        }

        private void AttachJoint(int index, Placeable otherObj, Vector3 point)
        {
            var myObj = GetComponent<Placeable>();
            var rbj = myObj.CreateRbJoint(otherObj);
            if (rbj.state == RbJoint.State.None)
            {
                rbj.SetupRb1(false);

                var awayDir = (point - transform.position).normalized * 0.1f;
                Vector3 p1 = point - awayDir;
                Vector3 p2 = point + awayDir;
                var otherRB = otherObj.Rigidbody;
                ref var cDesc = ref connections[index];
                cDesc.MyRbJ = rbj;

                var j = cDesc.Joint;
                if (!j)
                    j = CreateJoint(otherRB);
                j.anchor = transform.InverseTransformPoint(p1);
                j.connectedBody = otherRB;
                j.connectedAnchor = otherRB.transform.InverseTransformPoint(p2);
                j.breakForce = BreakForce;
                j.breakTorque = BreakTorque;
                cDesc.Joint = j;

                rbj.SetupRb2(j);
                rbj.EnableRbjCleanup();
            }
        }

        private int FindNextConnectable()
        {
            if (connections[lastConnection].Broken)
                DisconnectJoint(lastConnection);
            if (!connections[lastConnection].Connected)
                return lastConnection;
            WeekenJoint(connections[lastConnection].Joint);
            IncLastConnection();
            DisconnectJoint(lastConnection);
            return lastConnection;
        }

        private void IncLastConnection()
        {
            lastConnection++;
            if (lastConnection == connections.Length)
                lastConnection = 0;
        }

        private void WeekenJoint(SpringJoint j)
        {
            j.breakForce = WBreakForce;
            j.breakTorque = WBreakTorque;
        }

        private void DisconnectJoints()
        {
            for (int i = 0; i < connections.Length; i++)
            {
                DisconnectJoint(i);
            }
        }

        private void DisconnectJoint(int i) => connections[i].Disconnect();


        private SpringJoint CreateJoint(Rigidbody rb)
        {
            var j = gameObject.AddComponent<SpringJoint>();
            j.autoConfigureConnectedAnchor = false;
            j.spring = 1000;
            j.enableCollision = true;
            j.damper = 5;
            j.breakForce = BreakForce;
            j.breakTorque = BreakTorque;
            j.connectedBody = rb;
            return j;
        }

        void IConnector.Disconnect(Label label)
        {
            for (int f = 0; f < connections.Length; f++)
            {
                if (connections[f].OtherObj == label)
                    DisconnectJoint(f);
            }
        }

        void IHasRbJointCleanup.RbJointCleanup(RbJoint rbj)
        {
            for (int f = 0; f < connections.Length; f++)
            {
                if (connections[f].MyRbJ == rbj)
                {
                    connections[f].MyRbJ = null;
                    var j = connections[f].Joint;
                    if (j)
                        Destroy(j);
                }
            }
        }
    }
}
