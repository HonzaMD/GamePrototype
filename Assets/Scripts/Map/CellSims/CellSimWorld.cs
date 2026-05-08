using System;
using Unity.Collections;
using Unity.Jobs;

namespace Assets.Scripts.Map.CellSims
{
    public sealed class CellSimWorld : IDisposable
    {
        public readonly Map Map;
        public readonly int Width;
        public readonly int Height;

        public NativeArray<byte> material;

        public bool InitMode { get; private set; } = true;
        public JobHandle Pending;

        public CellSimWorld(Map map, int width, int height)
        {
            Map = map;
            Width = width;
            Height = height;
            material = new NativeArray<byte>(width * height, Allocator.Persistent);
        }

        public int Idx(int x, int y) => y * Width + x;

        public void EndInit()
        {
            Pending.Complete();
            InitMode = false;
        }

        public void Step()
        {
            if (InitMode)
                return;
        }

        public void Dispose()
        {
            Pending.Complete();
            if (material.IsCreated)
                material.Dispose();
        }
    }
}
