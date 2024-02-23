using Assets.Scripts.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Map
{
    public struct Cell
    {
        private Placeable first;
        public CellFlags Blocking { get; private set; }
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
            else
            {
                if (p == first)
                {
                    Debug.LogError("Duplikujes pridavani " + p.name);
                    return;
                }
                
                if (listInfo.ArrSelector == 0)
                {
                    var arr = CellList.ReserveData(2, out listInfo);
                    arr[0] = p;
                    Blocking = first.CellBlocking | p.CellBlocking;
                    TriggerAddTest(p, first, ksids);
                }
                else if (size == CellListInfo.MaxSize)
                {
                    // Throw Helper
                    throw new InvalidOperationException("Prekrocena maximalni kapacita bunky");
                }
                else
                {
                    var arr = CellList.GetData(ref listInfo, (byte)(size + 1));
                    size--;
                    arr[size] = p;
                    Blocking = first.CellBlocking | p.CellBlocking;
                    TriggerAddTest(p, first, ksids);

                    for (int f = 0; f < size; f++)
                    {
                        Blocking |= arr[f].CellBlocking;
                        TriggerAddTest(p, arr[f], ksids);
                        if (arr[f] == p)
                            Debug.LogError("Duplikujes pridavani !" + p.name);
                    }
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
                    Blocking = CellFlags.Free;
                    var arr = CellList.GetData(listInfo);
                    size--;

                    for (int f = 0; f < size; f++)
                    {
                        Blocking |= arr[f].CellBlocking;
                        TriggerRemoveTest(p, arr[f], ksids);
                    }

                    first = arr[size - 1];
                    CellList.DecSizeBy1(ref listInfo, arr);
                }
                else
                {
                    CellList.Free(ref listInfo);
                    first = null;
                    listInfo.Size = 0;
                    Blocking = CellFlags.Free;
                }
            }
            else if (size > 1)
            {
                Blocking = first.CellBlocking;
                TriggerRemoveTest(p, first, ksids);
                bool found = false;

                size--;
                var arr = CellList.GetData(listInfo);
                for (int f = 0; f < size; f++)
                {
                    if (arr[f] == p)
                    {
                        arr[f] = arr[size - 1];
                        found = true;
                    }
                    else
                    {
                        Blocking |= arr[f].CellBlocking;
                        TriggerRemoveTest(p, arr[f], ksids);
                    }
                }
                if (found)
                {
                    CellList.DecSizeBy1(ref listInfo, arr);
                }
                else
                {
                    Debug.LogError("Odebiras neco co tu neni " + p.name);
                }
            }
            else
            {
                Debug.LogError("Odebiras neco co tu neni " + p.name);
            }
        }

        public void RecomputeBlocking()
        {
            Blocking = CellFlags.Free;
            foreach (var placeable in this)
            {
                Blocking |= placeable.CellBlocking;
            }
        }

        public CellFlags BlockingExcept(Placeable exclude)
        {
            var ret = CellFlags.Free;
            foreach (var placeable in this)
            {
                if (placeable != exclude)
                    ret |= placeable.CellBlocking;
            }
            return ret;
        }

        public Enumerator GetEnumerator() => new Enumerator(listInfo, first);

        public ref struct Enumerator
        {
            private int index;
            private int size;
            private readonly Span<Placeable> arr;
            public Placeable Current { get; private set; }

            public Enumerator(CellListInfo info, Placeable first)
            {
                Current = first;
                index = -2;
                size = info.Size-1;
                if (size > 0)
                    arr = CellList.GetData(info);
                else
                    arr = default;
            }

            public bool MoveNext()
            {
                var ni = index + 1;
                if (ni < size)
                {
                    index = ni;
                    if (ni >= 0)
                        Current = arr[index];
                    return true;
                }
                return false;
            }
        }
    }
}
