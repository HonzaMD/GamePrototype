﻿using Assets.Scripts.Bases;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Stuff
{
    public class StickyBomb : MonoBehaviour, IHasCleanup, ICanActivate, ISimpleTimerConsumer, IConnector
    {
        private const float BreakForce = 600;
        private const float BreakTorque = 600;
        private const float WBreakForce = 540;
        private const float WBreakTorque = 540;


        private int activeTag;
        public Renderer Renderer;
        public Connectable[] connectables = new Connectable[4];
        private ConnDesc[] connections = new ConnDesc[2];
        private const int ArrOffest = 2;
        private int lastConnection;

        private struct ConnDesc
        {
            public Label Label;
            public SpringJoint Joint;
            public bool ActiveRBs;
            public bool Broken => !Joint;
            public bool Connected => Label;
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

        private void Start()
        {
            connectables[0].Init(() => DisconnectJoint(0));
            connectables[1].Init(() => DisconnectJoint(1));
            connectables[2].Init(() => DisconnectJoint(0));
            connectables[3].Init(() => DisconnectJoint(1));

            var rb = GetComponent<Rigidbody>();
            CreateJoint(connectables[0].gameObject, rb);
            CreateJoint(connectables[1].gameObject, rb);
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
        }

        public void Cleanup()
        {
            Deactivate();
        }

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
                if (IsAlreadyAttached(label))
                    return;
                int index = FindNextConnectable();
                if (index >= 0)
                    AttachJoint(index, label, collision.contacts[0].point);
            }
        }

        private bool IsAlreadyAttached(Label label)
        {
            for (int f = 0; f < connections.Length; f++)
            {
                if (connections[f].Label == label && !connections[f].Broken)
                    return true;
            }
            return false;
        }

        private void AttachJoint(int index, Label label, Vector3 point)
        {
            var awayDir = (point - transform.position).normalized * 0.1f;
            Vector3 p1 = point - awayDir;
            Vector3 p2 = point + awayDir;
            var otherRB = label.Rigidbody;
            ref var c = ref connections[index];
            c.ActiveRBs = otherRB;
            c.Label = label;
            if (otherRB)
            {
                var j = CreateJoint(gameObject, otherRB);
                j.anchor = transform.InverseTransformPoint(p1);
                j.connectedBody = otherRB;
                j.connectedAnchor = otherRB.transform.InverseTransformPoint(p2);
                c.Joint = j;

                var a = connectables[index + ArrOffest];
                a.transform.SetParent(label.ParentForConnections, true);
                a.gameObject.SetActive(true);
            }
            else
            {
                var j = connectables[index].GetComponent<SpringJoint>();
                if (!j)
                    j = CreateJoint(connectables[index].gameObject, GetComponent<Rigidbody>());
                j.transform.SetParent(label.ParentForConnections, true);
                j.anchor = j.transform.InverseTransformPoint(p2);
                j.connectedAnchor = transform.InverseTransformPoint(p1);
                j.breakForce = BreakForce;
                j.breakTorque = BreakTorque;
                j.gameObject.SetActive(true);
                c.Joint = j;
            }
        }

        private int FindNextConnectable()
        {
            if (connections[lastConnection].Broken)
                DisconnectJoint(lastConnection);
            if (!connections[lastConnection].Connected)
                return lastConnection;
            WeekenJoint(connections[lastConnection].Joint);
            lastConnection ^= 1;
            DisconnectJoint(lastConnection);
            return lastConnection;
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

        public void DisconnectJoint(int index)
        {
            ref var c = ref connections[index];
            if (c.Connected)
            {
                if (c.ActiveRBs)
                {
                    var joint = c.Joint;
                    if (joint)
                        Destroy(joint);
                    DisconnctConnectable(index + ArrOffest);
                }
                else
                {
                    DisconnctConnectable(index);
                    if (c.Broken)
                        CreateJoint(connectables[index].gameObject, GetComponent<Rigidbody>());
                }
                c = default;
            }
        }

        private SpringJoint CreateJoint(GameObject go, Rigidbody rb)
        {
            var j = go.AddComponent<SpringJoint>();
            j.autoConfigureConnectedAnchor = false;
            j.spring = 1000;
            j.enableCollision = true;
            j.damper = 5;
            j.breakForce = BreakForce;
            j.breakTorque = BreakTorque;
            j.connectedBody = rb;
            return j;
        }

        private void DisconnctConnectable(int index)
        {
            var c = connectables[index];
            c.gameObject.SetActive(false);
            c.transform.parent = transform;
            c.transform.localPosition = Vector3.zero;
        }

        void IConnector.Disconnect(Label label)
        {
            for (int f = 0; f < connections.Length; f++)
            {
                if (connections[f].Label == label)
                    DisconnectJoint(f);
            }
        }
    }
}