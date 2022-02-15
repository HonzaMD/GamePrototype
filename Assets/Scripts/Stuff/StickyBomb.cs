using Assets.Scripts.Bases;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Stuff
{
    public class StickyBomb : MonoBehaviour, IHasCleanup, ICanActivate, ISimpleTimerConsumer
    {
        private int activeTag;
        public Renderer Renderer;
        public Connectable[] connectables = new Connectable[4];
        private Label[] connectedLabels = new Label[2];
        private SpringJoint[] activeJoints = new SpringJoint[2];
        private const int ArrOffest = 2;

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
            connectables[0].Init(() => DicconnectJoint(0));
            connectables[1].Init(() => DicconnectJoint(1));
            connectables[2].Init(() => DicconnectJoint(0));
            connectables[3].Init(() => DicconnectJoint(1));
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
                int index = FindFreeConnectable();
                if (index >= 0)
                    AttachJoint(index, label, collision.contacts[0].point);
            }
        }

        private bool IsAlreadyAttached(Label label)
        {
            foreach (var l in connectedLabels)
                if (l == label)
                    return true;
            return false;
        }

        private void AttachJoint(int index, Label label, Vector3 point)
        {
            var awayDir = (point - transform.position).normalized * 0.1f;
            Vector3 p1 = point - awayDir;
            Vector3 p2 = point + awayDir;
            var otherRB = label.Rigidbody;
            if (otherRB)
            {
                var j = gameObject.AddComponent<SpringJoint>();
                j.anchor = transform.InverseTransformPoint(p1);
                j.autoConfigureConnectedAnchor = false;
                j.spring = 1000;
                j.connectedBody = otherRB;
                j.connectedAnchor = otherRB.transform.InverseTransformPoint(p2);
                j.enableCollision = true;
                activeJoints[index] = j;

                var c = connectables[index + ArrOffest];
                c.transform.SetParent(label.ParentForConnections, true);
                c.gameObject.SetActive(true);
            }
            else
            {
                var j = connectables[index].GetComponent<SpringJoint>();
                j.transform.SetParent(label.ParentForConnections, true);
                j.anchor = j.transform.InverseTransformPoint(p2);
                j.connectedAnchor = transform.InverseTransformPoint(p1);
                j.gameObject.SetActive(true);
            }
            connectedLabels[index] = label;
        }

        private int FindFreeConnectable()
        {
            for (int i = 0; i < connectedLabels.Length; i++)
            {
                if (connectedLabels[i] == null)
                    return i;
            }
            return -1;
        }

        private void DisconnectJoints()
        {
            for (int i = 0; i < connectedLabels.Length; i++)
            {
                DicconnectJoint(i);
            }
        }

        public void DicconnectJoint(int index)
        { 
            if (connectedLabels[index] != null)
            {
                connectedLabels[index] = null;
                DisconnctConnectable(index);
                DisconnctConnectable(index + ArrOffest);
                var j = activeJoints[index];
                if (j != null)
                {
                    Destroy(j);
                    activeJoints[index] = null;
                }
            }
        }

        private void DisconnctConnectable(int index)
        {
            var c = connectables[index];
            if (c.gameObject.activeSelf)
            {
                c.gameObject.SetActive(false);
                c.transform.parent = transform;
                c.transform.localPosition = Vector3.zero;
            }
        }
    }
}
