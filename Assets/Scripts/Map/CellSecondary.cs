using Assets.Scripts.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Map
{
    public struct CellSecondary : ICell
    {
        private Placeable first;
        private CellListInfo listInfo;

        public static CellSecondary Empty;

        public void Add(Placeable p, Ksids ksids)
        {
            int size = listInfo.Size;
            if (size == 0)
            {
                first = p;
                listInfo.Size = 1;
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
                    TriggerAddTest(p, first, ksids);

                    for (int f = 0; f < size; f++)
                    {
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
                    var arr = CellList.GetData(listInfo);
                    size--;

                    for (int f = 0; f < size; f++)
                    {
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
                }
            }
            else if (size > 1)
            {
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


        public Cell.Enumerator GetEnumerator() => new Cell.Enumerator(listInfo, first);

    }
}
