using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Assets.Scripts.Map.CellSims
{
    [BurstCompile]
    internal struct ElementMargolusJob : IJobParallelFor
    {
        [ReadOnly][NoAlias] public NativeArray<sbyte> EFront;
        [NoAlias][NativeDisableParallelForRestriction] public NativeArray<sbyte> EBack;
        [ReadOnly][NoAlias] public NativeArray<byte> Material;
        public int Width;
        public int BlocksX;
        public int2 Offset;
        public int StepMod4;

        public void Execute(int blockIdx)
        {
            int bx = blockIdx % BlocksX;
            int by = blockIdx / BlocksX;
            int x0 = Offset.x + bx * 2;
            int y0 = Offset.y + by * 2;

            int i00 = y0 * Width + x0;
            int i10 = i00 + 1;
            int i01 = i00 + Width;
            int i11 = i01 + 1;

            // Rotující priorita — kam padají postupně zbytkové tokeny.
            // Cyklus i00 → i10 → i11 → i01 přes 4 stepy.
            int p0, p1, p2, p3;
            switch (StepMod4)
            {
                case 0: p0 = i00; p1 = i10; p2 = i11; p3 = i01; break;
                case 1: p0 = i10; p1 = i11; p2 = i01; p3 = i00; break;
                case 2: p0 = i11; p1 = i01; p2 = i00; p3 = i10; break;
                default: p0 = i01; p1 = i00; p2 = i10; p3 = i11; break;
            }

            // canHoldElement: cell musí mít Dirt flag, aby přijala prvky.
            int dp0 = (Material[p0] & CellSimWorld.DirtMask) != 0 ? 1 : 0;
            int dp1 = (Material[p1] & CellSimWorld.DirtMask) != 0 ? 1 : 0;
            int dp2 = (Material[p2] & CellSimWorld.DirtMask) != 0 ? 1 : 0;
            int dp3 = (Material[p3] & CellSimWorld.DirtMask) != 0 ? 1 : 0;
            int T = dp0 + dp1 + dp2 + dp3;

            // T == 0 — žádná dirt buňka v bloku. Preserve state (initBack už zkopíroval Front).
            // Jednodušší než "tvař se jako by byla všude" a zároveň konzistentní s "sealed wall"
            // edge case z doc — tokeny v plně-non-dirt regionu zůstanou kde jsou.
            if (T == 0) return;

            int sum = (int)EFront[i00] + (int)EFront[i10] + (int)EFront[i01] + (int)EFront[i11];

            // Floor division — drží konzervaci i pro záporný sum (debt).
            // C# `/` rounduje k 0 → fix-up když rem vyjde záporný.
            int baseShare = sum / T;
            int rem = sum - baseShare * T;
            if (rem < 0) { rem += T; baseShare -= 1; }

            // Distribuce v rotující prioritě, non-dirt slot dostává 0.
            // Pro T == 4 redukuje na původní pravidlo (všechny dpX == 1).
            // Pro 0 < T < 4 obsah z non-dirt buněk přeteče do dirt sousedů (sum konzervovaný,
            // jednotlivé buňky ne — non-dirt explicitně zaokrouhlí na 0).
            int remLeft = rem;
            sbyte v0 = dp0 == 1 ? (sbyte)(baseShare + (remLeft > 0 ? 1 : 0)) : (sbyte)0;
            if (dp0 == 1 && remLeft > 0) remLeft--;
            sbyte v1 = dp1 == 1 ? (sbyte)(baseShare + (remLeft > 0 ? 1 : 0)) : (sbyte)0;
            if (dp1 == 1 && remLeft > 0) remLeft--;
            sbyte v2 = dp2 == 1 ? (sbyte)(baseShare + (remLeft > 0 ? 1 : 0)) : (sbyte)0;
            if (dp2 == 1 && remLeft > 0) remLeft--;
            sbyte v3 = dp3 == 1 ? (sbyte)(baseShare + (remLeft > 0 ? 1 : 0)) : (sbyte)0;

            EBack[p0] = v0;
            EBack[p1] = v1;
            EBack[p2] = v2;
            EBack[p3] = v3;
        }
    }
}
