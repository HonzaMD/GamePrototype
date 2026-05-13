using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Assets.Scripts.Map.CellSims
{
    [Flags]
    public enum MaterialFlags : byte
    {
        None = 0,
        // Dirt — buňka může nést prvky (Margolus element redistribute respektuje).
        // Volné horní bity rezervovány pro další flagy (Solid, BlocksWater, ...).
        BasicDirt = WaterBlock | Dirt,

        WaterBlock = 1 << 6,
        Dirt = 1 << 7,
    }

    public enum MaterialChangeType
    {
        None,
        Dirt1to1Add,
        Dirt1to1Remove,
        Dirt0to1,
        Dirt1to0,
    }

    public sealed class CellSimWorld : IDisposable
    {
        public const byte DirtMask = (byte)MaterialFlags.Dirt;

        public readonly Map Map;
        public readonly int Width;
        public readonly int Height;

        // Material byte = flagy v horních bitech | ID v dolních bitech.
        // 2-buffer model: gameplay R/W přes materialPublic (instant visibility, vč. SetMaterial
        // returns prev). Sim job čte materialPrivate — snapshot Public, který se kopíruje na
        // začátku každého stepu (CellSimCopyByteJob, parallel Burst). Sim materiál nemění,
        // takže není potřeba reconcile.
        public NativeArray<byte> materialPublic;
        public NativeArray<byte> materialPrivate;

        // Element field — diskrétní hmota přes Margolus block rule.
        // 3-buffer model: gameplay R/W přes elementPublic, sim si snapshotne Public→Front,
        // pojede Front→Back a po stepu reconcileuje: Public += Back - Front.
        public NativeArray<sbyte> elementPublic;
        private NativeArray<sbyte> elementFront;
        private NativeArray<sbyte> elementBack;

        public bool InitMode { get; private set; } = true;
        public JobHandle Pending;

        private int stepCounter;

        public CellSimWorld(Map map, int width, int height)
        {
            Map = map;
            Width = width;
            Height = height;
            int n = width * height;
            materialPublic = new NativeArray<byte>(n, Allocator.Persistent);
            materialPrivate = new NativeArray<byte>(n, Allocator.Persistent);
            elementPublic = new NativeArray<sbyte>(n, Allocator.Persistent);
            elementFront = new NativeArray<sbyte>(n, Allocator.Persistent);
            elementBack = new NativeArray<sbyte>(n, Allocator.Persistent);
        }

        public int Idx(int x, int y) => y * Width + x;
        public int Idx(Vector2Int pos) => pos.y * Width + pos.x;

        // ---- Element R/W API (Vector2Int souřadnice, bounds-checked, clamped) ----

        public int GetElement(Vector2Int pos)
        {
            if ((uint)pos.x >= (uint)Width || (uint)pos.y >= (uint)Height) return 0;
            int v = elementPublic[pos.y * Width + pos.x];
            return v < 0 ? 0 : v;
        }

        public void SetElement(Vector2Int pos, int value)
        {
            if ((uint)pos.x >= (uint)Width || (uint)pos.y >= (uint)Height) return;
            elementPublic[pos.y * Width + pos.x] = (sbyte)math.clamp(value, sbyte.MinValue, sbyte.MaxValue);
        }

        public void AddElement(Vector2Int pos, int delta)
        {
            if ((uint)pos.x >= (uint)Width || (uint)pos.y >= (uint)Height) return;
            int idx = pos.y * Width + pos.x;
            int v = elementPublic[idx] + delta;
            elementPublic[idx] = (sbyte)math.clamp(v, sbyte.MinValue, sbyte.MaxValue);
        }

        public byte SetMaterial(Vector2Int pos, byte newMat)
        {
            if ((uint)pos.x >= (uint)Width || (uint)pos.y >= (uint)Height) return 0;
            int idx = pos.y * Width + pos.x;
            byte prev = materialPublic[idx];
            materialPublic[idx] = newMat;
            return prev;
        }

        public byte GetMaterial(Vector2Int pos)
        {
            if ((uint)pos.x >= (uint)Width || (uint)pos.y >= (uint)Height) return 0;
            int idx = pos.y * Width + pos.x;
            return materialPublic[idx];
        }

        // ---- Lifecycle ----

        public void EndInit()
        {
            Pending.Complete();
            InitMode = false;
        }

        public void Step()
        {
            if (InitMode) return;

            int n = Width * Height;

            // 1. Sync část — blokuje main thread, ale jen krátce. Definuje deterministický
            //    "snapshot moment": vše zapsané do Public bufferů PŘED Step() vidí tento step
            //    simulace; cokoli zapsané PO Step() jde do dalšího stepu. Bez tohoto sync
            //    snapshotu by gameplay zápisy během async fáze racovaly s copy/reconcile joby.
            //    a) Dokončíme Margolus z minulého stepu (Back má sim výstup, Front starý snapshot).
            //    b) Reconcile + nový snapshot elementů (Pub_new = Pub + Back - Front, Front_new = Pub_new).
            //    c) Snapshot materialPublic → materialPrivate.
            //    Oba sync joby běží paralelně (disjunktní buffery).
            Pending.Complete();
            var reconcile = new CellSimReconcileSnapshotSByteJob
            {
                Back = elementBack,
                Front = elementFront,
                Pub = elementPublic,
            }.Schedule(n, 1024);
            var copyMaterial = new CellSimCopyByteJob
            {
                Src = materialPublic,
                Dst = materialPrivate,
            }.Schedule(n, 1024);
            JobHandle.CombineDependencies(reconcile, copyMaterial).Complete();

            // 2. Async část — main thread je volný, gameplay může R/W Public buffery.
            //    a) Init Back = Front (border cells netknuté Margolusem mají v příštím
            //       reconcile delta=0, když Width/Height nedělitelné offsetem → 1-cell pruh).
            //    b) Margolus block step.
            var initBack = new CellSimCopySByteJob { Src = elementFront, Dst = elementBack }
                .Schedule(n, 1024);

            int2 offset = (stepCounter & 1) == 0 ? new int2(0, 0) : new int2(1, 1);
            int blocksX = (Width - offset.x) / 2;
            int blocksY = (Height - offset.y) / 2;
            int blockCount = blocksX * blocksY;

            JobHandle margolus = new ElementMargolusJob
            {
                EFront = elementFront,
                EBack = elementBack,
                Material = materialPrivate,
                Width = Width,
                BlocksX = blocksX,
                Offset = offset,
                StepMod4 = stepCounter & 3,
            }.Schedule(blockCount, 64, initBack);

            Pending = margolus;
            stepCounter++;
        }

        public void Dispose()
        {
            Pending.Complete();
            if (materialPublic.IsCreated) materialPublic.Dispose();
            if (materialPrivate.IsCreated) materialPrivate.Dispose();
            if (elementPublic.IsCreated) elementPublic.Dispose();
            if (elementFront.IsCreated) elementFront.Dispose();
            if (elementBack.IsCreated) elementBack.Dispose();
        }
    }
}
