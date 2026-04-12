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

        [Header("Koreny")]
        public float RootUpExponent = 3f;
        public float UndergroundBalanceWeight = -1.3f;
        public float BalanceWeight = 1f;

        [Header("Fyzika")]
        public float BaseMass = 1.2f;
        public float BaseSpStretchLimit = 40f;
        public float BaseSpCompressLimit = 40f;
        public float BaseSpMomentLimit = 25f;

        [Header("Tloustka")]
        public int MaxThicknessLevel = 5;
        [Tooltip("Scale XZ = ScaleConstant + level * ScalePerLevel")]
        public float ScaleConstant = 0.03f;
        public float ScalePerLevel = 0.07f;

        public float GetGrowthDelay() => GrowthInterval + Random.Range(-GrowthIntervalRandom, GrowthIntervalRandom);

        public int GetThicknessLevel(int segmentCount)
        {
            float ratio = (float)segmentCount / MaxSegments;
            int level = Mathf.CeilToInt(ratio * MaxThicknessLevel);
            return Mathf.Clamp(level, 1, MaxThicknessLevel);
        }
    }
}
