using UnityEngine;

namespace Assets.Scripts.Stuff
{
    [CreateAssetMenu]
    public class TreeSettings : ScriptableObject
    {
        [Header("Rust")]
        public float GrowthInterval = 5f;
        public float GrowthIntervalRandom = 1f;
        public int MaxSegments = 50;

        [Header("Fyzika")]
        public float BaseMass = 20f;
        public float BaseSpStretchLimit = 800f;
        public float BaseSpCompressLimit = 800f;
        public float BaseSpMomentLimit = 500f;

        public float GetGrowthDelay() => GrowthInterval + Random.Range(-GrowthIntervalRandom, GrowthIntervalRandom);
    }
}
