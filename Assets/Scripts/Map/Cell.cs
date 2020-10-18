using Assets.Scripts.Core;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Assets.Scripts.Map
{
    public struct Cell
    {
        private Placeable first;
        public CellBlocking Blocking { get; private set; }       
        private CellListInfo listInfo;

        public static Cell Empty;

        public void Add(Placeable p, Ksids ksids)
        {
            int size = listInfo.Size;
            if (size == 0)
            {
                first = p;
                listInfo.Size = 1;
                Blocking = p.CellBlocking;
            }
            else if (size == 1)
            {
                var arr = CellList.ReserveData(2, out listInfo, out int offset);
                arr[offset] = p;
                Blocking = first.CellBlocking | p.CellBlocking;
                TriggerAddTest(p, first, ksids);
            }
            else if (size > CellList.maxCapacity)
            {
                // Throw Helper
                throw new InvalidOperationException("Prekrocena maximalni kapacita bunky");
            }
            else
            {
                CellList.IncSize(ref listInfo, size + 1);
                var arr = CellList.GetData(listInfo, out int offset);
                size--;
                arr[offset + size] = p;
                Blocking = first.CellBlocking | p.CellBlocking;
                TriggerAddTest(p, first, ksids);

                for (int f = offset; f < offset + size; f++)
                {
                    Blocking |= arr[f].CellBlocking;
                    TriggerAddTest(p, arr[f], ksids);
                }
            }
        }

        private void TriggerAddTest(Placeable p1, Placeable p2, Ksids ksids)
        {
            if (p2.IsTrigger && ksids.IsParentOrEqual(p1.Ksid, p2.TriggerTargets))
                p2.AddTarget(p1);
            if (p1.IsTrigger && ksids.IsParentOrEqual(p2.Ksid, p1.TriggerTargets))
                p1.AddTarget(p2);
        }

        private void TriggerRemoveTest(Placeable p1, Placeable p2, Ksids ksids)
        {
            if (p2.IsTrigger && ksids.IsParentOrEqual(p1.Ksid, p2.TriggerTargets))
                p2.RemoveTarget(p1);
            if (p1.IsTrigger && ksids.IsParentOrEqual(p2.Ksid, p1.TriggerTargets))
                p1.RemoveTarget(p2);
        }

        public void Remove(Placeable p, Ksids ksids)
        {
            int size = listInfo.Size;
            if (first == p)
            {
                if (size > 1)
                {
                    Blocking = CellBlocking.Free;
                    var arr = CellList.GetData(listInfo, out int offset);
                    size--;

                    for (int f = offset; f < offset + size; f++)
                    {
                        Blocking |= arr[f].CellBlocking;
                        TriggerRemoveTest(p, arr[f], ksids);
                    }

                    first = arr[offset + size - 1];
                    CellList.DecSize(ref listInfo, size);
                }
                else
                {
                    first = null;
                    listInfo.Size = 0;
                    Blocking = CellBlocking.Free;
                }
            }
            else if (size > 1)
            {
                Blocking = first.CellBlocking;
                TriggerRemoveTest(p, first, ksids);

                size--;
                var arr = CellList.GetData(listInfo, out int offset);
                for (int f = offset; f < offset + size; f++)
                {
                    if (arr[f] == p)
                    {
                        arr[f] = arr[offset + size - 1];
                    }
                    else
                    {
                        Blocking |= arr[f].CellBlocking;
                        TriggerRemoveTest(p, arr[f], ksids);
                    }
                }
                CellList.DecSize(ref listInfo, size);
            }
        }

        public void RecomputeBlocking()
        {
            Blocking = CellBlocking.Free;
            foreach (var placeable in this)
            {
                Blocking |= placeable.CellBlocking;
            }
        }

        public CellBlocking BlockingExcept(Placeable exclude)
        {
            var ret = CellBlocking.Free;
            foreach (var placeable in this)
            {
                if (placeable != exclude)
                    ret |= placeable.CellBlocking;
            }
            return ret;
        }

        public Enumerator GetEnumerator() => new Enumerator(listInfo, first);

        public struct Enumerator
        {
            private int index;
            private readonly Placeable[] arr;
            private readonly int start;
            private readonly int end;
            private readonly Placeable first;

            public Enumerator(CellListInfo info, Placeable first)
            {
                this.first = first;
                var size = info.Size;
                if (size > 1)
                {
                    arr = CellList.GetData(info, out var i);
                    start = i - 1;
                    index = i - 2;
                    end = i + size - 1;
                }
                else
                {
                    arr = null;
                    start = 0;
                    index = -1;
                    end = size;
                }
            }

            public bool MoveNext()
            {
                var ni = index + 1;
                if (ni < end)
                {
                    index = ni;
                    return true;
                }
                return false;
            }

            public Placeable Current => index == start ? first : arr[index];
        }
    }
}
