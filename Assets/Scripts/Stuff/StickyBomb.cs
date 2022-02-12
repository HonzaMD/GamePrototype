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
    public class StickyBomb : MonoBehaviour, IHasCleanup, ICanActivate, IConnectable, IPhysicsEvents, ISimpleTimerConsumer
    {
        private int activeTag;
        public Renderer Renderer;

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
        }

        public void OnCollisionEnter(Collision collision)
        {
            if (IsActive && Label.TryFind(collision.collider.transform, out var label))
            {
                var p = GetComponent<Placeable>();
                p.DetachRigidBody();
                transform.SetParent(label.ParentForConnections, true);
            }
        }

        public void Disconnect()
        {
            var p = GetComponent<Placeable>();
            transform.SetParent(p.LevelGroup, true);
            p.AttachRigidBody();
        }
    }
}
