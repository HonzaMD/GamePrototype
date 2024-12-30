using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Map
{
    public struct CellListInfo
    {
        public const int MaxSize = 65;

        public ushort ArrSelector; // 0 znamena NULL
        public byte ArrPtr;
        public byte Size; // bacha prvni prvek je mimo arr, takze size je jakoby o 1 vetsi

        public CellListInfo(ushort arrSelector, byte arrPtr, byte size)
        {
            ArrSelector = arrSelector;
            ArrPtr = arrPtr;
            Size = size;
        }
    }


    static class CellList
    {
        private const int sizeShift = 8;
        public const int maxCapacity = CellListInfo.MaxSize - 1; // 64

        private readonly static List<Placeable[]> data = new() { null };
        private readonly static (Queue<CellListInfo> List, CellListInfo LastGi)[] freeLists = InitFreeLists();
        private static readonly int[] sizeToCapacity = InitSizeToCapacity();

        public static void CheckEmpty()
        {
            if (data.Count != 1)
                throw new InvalidOperationException("Cekal jsem ze CellList bude prazdny");
            if (freeLists.Length != maxCapacity + 1)
                throw new InvalidOperationException("Cekal jsem ze CellList bude prazdny 2");
            if (freeLists[2].LastGi.ArrSelector != 0 || freeLists[2].List.Count > 0)
                throw new InvalidOperationException("Cekal jsem ze CellList bude prazdny 2");
        }

        private static (Queue<CellListInfo> List, CellListInfo LastGi)[] InitFreeLists()
        {
            var ret = new (Queue<CellListInfo> List, CellListInfo LastGi)[maxCapacity + 1];
            int value = 2;
            while (value <= maxCapacity)
            {
                ret[value].List = new ();
                value *= 2;
            }
            return ret;
        }

        private static int[] InitSizeToCapacity()
        {
            var ret = new int[maxCapacity + 2];
            int cap = 0;
            for (int f = 0; f < ret.Length; f++)
            {
                if (f == 2)
                {
                    cap = 2;
                }
                else if (f - 1 > cap)
                {
                    cap *= 2;
                }
                ret[f] = cap;
            }
            return ret;
        }

        
        public static Span<Placeable> GetData(CellListInfo info)
        {
            var arr = data[info.ArrSelector];
            int capacity = arr.Length >> sizeShift;
            return arr.AsSpan(info.ArrPtr * capacity, capacity);
        }

        private static Span<Placeable> PopData(CellListInfo info)
        {
            var arr = data[info.ArrSelector];
            int capacity = arr.Length >> sizeShift;
            freeLists[capacity].List.Enqueue(info);
            return arr.AsSpan(info.ArrPtr * capacity, capacity);
        }

        public static Span<Placeable> GetData(ref CellListInfo info, byte newSize)
        {
            var arr = data[info.ArrSelector];
            int capacity = arr.Length >> sizeShift;
            if (newSize - 1 > capacity)
            {
                return RelocateAndGet(ref info, newSize);
            }
            else
            {
                info.Size = newSize;
                return arr.AsSpan(info.ArrPtr * capacity, capacity);
            }
        }


        //public static void IncSize(ref CellListInfo info, int newSize)
        //{
        //    int gi = info.Index;
        //    int capacity = data[gi >> blockShift].Capacity;
        //    if (newSize - 1 > capacity)
        //    {
        //        Relocate(ref info, gi, newSize);
        //        freeLists[capacity].List.Enqueue(gi);
        //    }
        //    else
        //    {
        //        info.Size = newSize;
        //    }
        //}

        public static void DecSizeBy1(ref CellListInfo info, Span<Placeable> currData)
        {
            int capacity = currData.Length;
            int newSize = --info.Size;
            currData[newSize-1] = null;

            if (newSize < capacity >> 1)
            {
                freeLists[capacity].List.Enqueue(info);
                if (newSize <= 1)
                {
                    info.ArrSelector = 0;
                }
                else
                {
                    RelocateDown(ref info, currData);
                }
            }
        }

        public static void DecSize(ref CellListInfo info, byte newSize, Span<Placeable> currData)
        {
            int capacity = currData.Length;
            ClearArr(ref info, newSize, currData);

            if (newSize < capacity >> 1)
            {
                freeLists[capacity].List.Enqueue(info);
                if (newSize <= 1)
                {
                    info.ArrSelector = 0;
                }
                else
                {
                    RelocateDown(ref info, currData);
                }
            }
        }

        internal static void Free(ref CellListInfo info)
        {
            if (info.ArrSelector != 0)
            {
                var capacity = data[info.ArrSelector].Length >> sizeShift;
                freeLists[capacity].List.Enqueue(info);
                info.ArrSelector = 0;
            }
        }

        private static void ClearArr(ref CellListInfo info, byte newSize, Span<Placeable> currData)
        {
            int sizeDelta = info.Size - newSize;
            int clearStart;
            if (newSize > 0)
            {
                clearStart = newSize - 1;
            }
            else
            {
                clearStart = 0;
                sizeDelta--;
            }
            currData.Slice(clearStart, sizeDelta).Clear();
            info.Size = newSize;
        }

       
        private static void RelocateDown(ref CellListInfo info, Span<Placeable> src)
        {
            int size = info.Size - 1;
            src = src.Slice(0, size);
            var dest = ReserveData(info.Size, out info);

            src.CopyTo(dest);
            src.Clear();
        }

        private static Span<Placeable> RelocateAndGet(ref CellListInfo info, byte newSize)
        {
            int size = info.Size - 1;
            var src = PopData(info).Slice(0, size);
            var dest = ReserveData(newSize, out info);

            src.CopyTo(dest);
            src.Clear();

            return dest;
        }


        public static Span<Placeable> ReserveData(byte size, out CellListInfo info)
        {
            info = GetFreeIndex(size);
            return GetData(info);
        }

        private static CellListInfo GetFreeIndex(byte size)
        {
            int capacity = sizeToCapacity[size];
            CellListInfo gi;
            ref var freeInfo = ref freeLists[capacity];

            if (freeInfo.List.Count > 0)
            {
                gi = freeInfo.List.Dequeue();
            }
            else
            {
                gi = freeInfo.LastGi;
                if (gi.ArrPtr == 0)
                {
                    gi = AllocNewBloc(capacity);
                    freeInfo.LastGi = gi;
                }

                freeInfo.LastGi.ArrPtr++;
            }

            gi.Size = size;
            return gi;
        }

        private static CellListInfo AllocNewBloc(int capacity)
        {
            CellListInfo gi = new((ushort)data.Count, 0, 0);
            if (gi.ArrSelector == 0)
                ThrowOutOfMemory();
            data.Add(new Placeable[capacity << sizeShift]);
            return gi;
        }

        private static void ThrowOutOfMemory()
        {
            throw new InvalidOperationException("Dosla pamet v CellList strukturach");
        }
    }
}
