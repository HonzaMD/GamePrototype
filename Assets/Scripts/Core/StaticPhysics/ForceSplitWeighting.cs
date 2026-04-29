using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Core.StaticPhysics
{
    internal static class ForceSplitWeighting
    {
        // Vazici parametry pro kombinaci delka x pevnost (varianta 2b: (wLen+EpsLen)*(wStr+EpsStr)).
        // 0 = ciste multiplikativni (silny kontrast), vetsi = mekci (blizsi aritm. prumeru).
        private const float EpsLen = 0.05f;
        private const float EpsStr = 0.1f;

        // Vraci num/sum, ale pri 0/0 nebo Inf/Inf (NaN) vrati 1/count jako uniformni fallback.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float SafeWeight(float num, float sum, int count)
        {
            float w = num / sum;
            return float.IsNaN(w) ? 1f / count : w;
        }

        // Kombinovana neznormalizovana vaha jedne cesty: (wLen+epsLen)*(wStr+epsStr).
        // Sdileno mezi sumovaci fazi (GetCombinedSums) a aplikacni (ComputeWeight).
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float CombinedP(float invLen, float strength, float invLenSum, float strSum, int count)
        {
            float wLen = SafeWeight(invLen, invLenSum, count);
            float wStr = SafeWeight(strength, strSum, count);
            return (wLen + EpsLen) * (wStr + EpsStr);
        }

        // Finalni normalizovana vaha cesty (sum_i = 1).
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float ComputeWeight(float invLen, float strength, in WeightSums s)
        {
            float p = CombinedP(invLen, strength, s.invLenSum, s.strSum, s.count);
            return SafeWeight(p, s.pSum, s.count);
        }

        public static (float W1, float W2) Compute2Weights(float length1, float length2, float strength1, float strength2)
        {
            float invLen1 = 1f / length1;
            float invLen2 = 1f / length2;
            float invLenSum = invLen1 + invLen2;
            float strSum = strength1 + strength2;
            float p1 = CombinedP(invLen1, strength1, invLenSum, strSum, 2);
            float p2 = CombinedP(invLen2, strength2, invLenSum, strSum, 2);
            var sums = new WeightSums(invLenSum, strSum, p1 + p2, 2);

            return (ComputeWeight(invLen1, strength1, sums), ComputeWeight(invLen2, strength2, sums));
        }
    }


    // Predpocitane sumy potrebne pro vazene rozdeleni sily mezi vystupnimi cestami jedne barvy.
    // Sumy mohou byt Infinity (delka 0 -> 1/0) nebo 0 (zadne pevnosti); spravne se s tim vyporada SafeWeight.
    internal readonly struct WeightSums
    {
        public readonly float invLenSum;
        public readonly float strSum;
        public readonly float pSum;
        public readonly int count;

        public WeightSums(float invLenSum, float strSum, float pSum, int count)
        {
            this.invLenSum = invLenSum;
            this.strSum = strSum;
            this.pSum = pSum;
            this.count = count;
        }
    }
}
