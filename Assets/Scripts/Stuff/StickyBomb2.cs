using Assets.Scripts.Bases;
using Assets.Scripts.Utils;
using UnityEngine;

namespace Assets.Scripts.Stuff
{
    public class StickyBomb2 : StickyRingBase, ICanActivate, ISimpleTimerConsumer
    {
        private int activeTag;
        public Renderer Renderer;

        int ISimpleTimerConsumer.ActiveTag { get => activeTag; set => activeTag = value; }
        private bool IsActive => (activeTag & 1) != 0;

        protected override bool IsSticky => IsActive;

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

        protected override void OnCleanup(bool goesToInventory)
        {
            Debug.Assert(!goesToInventory, "Nepodporuju imistovani do inventare");
            if (IsActive)
            {
                activeTag++;
                SetShader(0, 0, 1);
            }
        }
    }
}
