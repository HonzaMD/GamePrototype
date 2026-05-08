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
        [WriteOnly][NoAlias][NativeDisableParallelForRestriction] public NativeArray<sbyte> EBack;
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

            int sum = (int)EFront[i00] + (int)EFront[i10] + (int)EFront[i01] + (int)EFront[i11];
            int baseShare = sum >> 2;            // arith shift → round to -inf, drží konzervaci pro záporné sum
            int rem = sum - baseShare * 4;       // 0..3

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

            EBack[p0] = (sbyte)(baseShare + (rem > 0 ? 1 : 0));
            EBack[p1] = (sbyte)(baseShare + (rem > 1 ? 1 : 0));
            EBack[p2] = (sbyte)(baseShare + (rem > 2 ? 1 : 0));
            EBack[p3] = (sbyte)(baseShare);
        }
    }
}
