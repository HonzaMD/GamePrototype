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

        [Header("Vetveni")]
        public float BranchWeight = 0.3f;
        public float NearBranchWeight = 0.2f;
        public float EndBranchPenalty = 0.2f;
        [Tooltip("Jak moc preferovat rust blizko X stredu stromu. 0 = zadna preference, vetsi = silnejsi preference")]
        public float CenterPreference = 1f;
        [Tooltip("Exponent pro preferenci rustu nahoru. 1 = linearni, vetsi = silnejsi preference nahoru")]
        public float UpExponent = 3f;

        [Header("Fyzika")]
        public float BaseMass = 20f;
        public float BaseSpStretchLimit = 800f;
        public float BaseSpCompressLimit = 800f;
        public float BaseSpMomentLimit = 500f;

        public float GetGrowthDelay() => GrowthInterval + Random.Range(-GrowthIntervalRandom, GrowthIntervalRandom);
    }
}
