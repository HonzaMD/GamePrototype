using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Map.CellSims;
using Assets.Scripts.Utils;
using System;
using UnityEngine;

namespace Assets.Scripts.Stuff
{
    [RequireComponent(typeof(PlaceableSibling))]
    public class ElementMedium : MonoBehaviour, IHasCleanup, IHasDirtFlagChange
    {
        private int elementCount;

        public void Cleanup(bool goesToInventory)
        {
            elementCount = 0;
        }

        public void DirtFlagChange(Map.Map map, Vector2Int cellPos, MaterialChangeType changeType, Placeable placeableSibling)
        {
            switch (changeType)
            {
                case MaterialChangeType.Dirt0to1:
                case MaterialChangeType.Dirt1to1Add:
                    if (elementCount > 0)
                    {
                        map.CellSim.AddElement(cellPos, elementCount);
                        elementCount = 0;
                    }
                    break;
                case MaterialChangeType.Dirt1to0:
                    {
                        var removed = map.CellSim.GetElement(cellPos);
                        if (removed > 0)
                            map.CellSim.AddElement(cellPos, -removed);
                        elementCount += removed;
                    }
                    break;
                default:
                    break;
            }
        }
    }
}
