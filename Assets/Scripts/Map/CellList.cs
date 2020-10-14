using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Map
{
    public struct CellListInfo
    {
        private const int SizeMask = 0x3F;
        
        int indexAndSize;

        public int Index
        {
            get => (indexAndSize >> 6) << 1;
            set => indexAndSize = (value >> 1) << 6 | (indexAndSize & 0x3F);
        }

        public int Size
        {
            get => indexAndSize & SizeMask;
            set => indexAndSize = (indexAndSize & ~SizeMask) | value;
        }

        public CellListInfo(int index, int size)
        {
            indexAndSize = (index >> 1) << 6 | size;
        }
    }


    static class CellList
    {
        private const int blockSize = 32 * 1024;
        private const int blockMask = blockSize - 1;
        private const int blockShift = 15;
        public const int maxCapacity = 32;
        private readonly static List<(Placeable[] Arr, int Capacity)> data = new List<(Placeable[], int)>();
        private readonly static (Queue<int> List, int LastGi)[] freeLists = InitFreeLists();
        private static readonly int[] sizeToCapacity = InitSizeToCapacity();

        public static void CheckEmpty()
        {
            if (data.Count != 0)
                throw new InvalidOperationException("Cekal jsem ze CellList bude prazdny");
            if (freeLists.Length != maxCapacity + 1)
                throw new InvalidOperationException("Cekal jsem ze CellList bude prazdny 2");
            if (freeLists[2].LastGi != 0 || freeLists[2].List.Count > 0)
                throw new InvalidOperationException("Cekal jsem ze CellList bude prazdny 2");
        }

        private static (Queue<int> List, int LastGi)[] InitFreeLists()
        {
            var ret = new (Queue<int> List, int LastGi)[maxCapacity + 1];
            int value = 2;
            while (value <= maxCapacity)
            {
                ret[value].List = new Queue<int>();
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

        public static Placeable[] GetData(CellListInfo info, out int index)
        {
            return GetData(info.Index, out index);
        }

        public static Placeable[] GetData(int gi, out int index)
        {
            index = gi & blockMask;
            return data[gi >> blockShift].Arr;
        }

        public static Placeable[] ReserveData(int size, out CellListInfo info, out int index)
        {
            int gi = GetFreeIndex(size);
            info = new CellListInfo(gi, size);
            return GetData(gi, out index);
        }

        private static int GetFreeIndex(int size)
        {
            int capacity = sizeToCapacity[size];

            if (freeLists[capacity].List.Count > 0)
                return freeLists[capacity].List.Dequeue();

            int gi = freeLists[capacity].LastGi;
            if ((gi & blockMask) == 0)
            {
                gi = AllocNewBloc(capacity);
            }

            freeLists[capacity].LastGi = gi + capacity;

            return gi;
        }

        private static int AllocNewBloc(int capacity)
        {
            int gi = data.Count * blockSize;
            data.Add((new Placeable[blockSize], capacity));
            return gi;
        }


        public static void IncSize(ref CellListInfo info, int newSize)
        {
            int gi = info.Index;
            int capacity = data[gi >> blockShift].Capacity;
            if (newSize - 1 > capacity)
            {
                Relocate(ref info, gi, newSize);
                freeLists[capacity].List.Enqueue(gi);
            }
            else
            {
                info.Size = newSize;
            }
        }

        public static void DecSize(ref CellListInfo info, int newSize)
        {
            int gi = info.Index;
            int capacity = data[gi >> blockShift].Capacity;

            ClaerArr(newSize, ref info);

            if (newSize <= 1)
            {
                info.Index = -1;
                freeLists[capacity].List.Enqueue(gi);
            }
            else if (capacity << 2 >= 2 && newSize - 1 <= capacity << 2)
            {
                Relocate(ref info, gi, newSize);
                freeLists[capacity].List.Enqueue(gi);
            }
        }

        private static void ClaerArr(int newSize, ref CellListInfo info)
        {
            var arr = GetData(info, out var index);
            var size = info.Size;
            var start = newSize == 0 ? 1 : newSize;
            index--;

            for (int i = start; i < size; i++)
            {
                arr[index + i] = null;
            }

            info.Size = newSize;
        }

        private static void Relocate(ref CellListInfo info, int gi, int newSize)
        {
            int size = info.Size - 1;
            var src = GetData(gi, out int srcIndex);
            var dest = ReserveData(newSize, out info, out int destIndex);

            for (int f = 0; f < size; f++)
            {
                dest[destIndex + f] = src[srcIndex + f];
                src[srcIndex + f] = null;
            }
        }
    }
}
