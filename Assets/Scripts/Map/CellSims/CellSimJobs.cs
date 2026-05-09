using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Assets.Scripts.Map.CellSims
{
    public struct MaterialWrite
    {
        public int Idx;
        public byte NewMat;
    }

    [BurstCompile]
    internal struct CellSimCopySByteJob : IJobParallelFor
    {
        [ReadOnly][NoAlias] public NativeArray<sbyte> Src;
        [WriteOnly][NoAlias] public NativeArray<sbyte> Dst;
        public void Execute(int i) => Dst[i] = Src[i];
    }

    // Drain materialWrites queue do material bufferu. Single-threaded — řídké zápisy,
    // schedule overhead by se nevyplatil; volá se .Run() inline na začátku stepu.
    [BurstCompile]
    internal struct ApplyMaterialWritesJob : IJob
    {
        public NativeQueue<MaterialWrite> Queue;
        [NoAlias] public NativeArray<byte> Material;

        public void Execute()
        {
            while (Queue.TryDequeue(out var w))
                Material[w.Idx] = w.NewMat;
        }
    }

    // Combined reconcile + snapshot — jeden lineární průchod místo dvou.
    //   Pub_new   = Pub + (Back - Front)    ... reconcile sim delty z minulého stepu
    //   Front_new = Pub_new                  ... snapshot pro Margolus aktuálního stepu
    // Běží sync na začátku Step (čte/píše Pub i Front, gameplay nesmí v této fázi sahat).
    [BurstCompile]
    internal struct CellSimReconcileSnapshotSByteJob : IJobParallelFor
    {
        [ReadOnly][NoAlias] public NativeArray<sbyte> Back;
        [NoAlias] public NativeArray<sbyte> Front;
        [NoAlias] public NativeArray<sbyte> Pub;

        public void Execute(int i)
        {
            int v = (int)Pub[i] + (int)Back[i] - (int)Front[i];
            sbyte newPub = (sbyte)math.clamp(v, sbyte.MinValue, sbyte.MaxValue);
            Pub[i] = newPub;
            Front[i] = newPub;
        }
    }
}
