using Assets.Scripts.Bases;
using Assets.Scripts.Core.Inventory;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Stuff
{
    [RequireComponent(typeof(PlaceableSibling), typeof(IHasInventory))]
    public class InventorySearcher : MonoBehaviour, IHasAfterMapPlaced, IHasCleanup, IActiveObject1Sec
    {
        private Map.Map map;
        private IHasInventory hasInventory;
        private Placeable placeableSibling;

        private static readonly Vector2 mainStart = new Vector2(-1f, -1f);
        private static readonly Vector2 mainSize = mainStart * -2;
        private static readonly Vector2 secondaryStart = new Vector2(-7f, -7f);
        private static readonly Vector2 secondarySize = secondaryStart * -2;
        private static readonly float maximumSqrDistance = secondaryStart.x * secondaryStart.x;
        private static readonly Inventory[] linkArrNew = new Inventory[3];
        private static readonly Inventory[] linkArrOld = new Inventory[3];

        private void Awake()
        {
            hasInventory = GetComponent<IHasInventory>();
        }

        public void GameUpdate1Sec()
        {
            int tag = map.GetNextTag();
            var list = ListPool<Placeable>.Rent();
            var center = placeableSibling.Center;
            map.Get(list, center + mainStart, mainSize, Core.Ksid.HasInventory, tag);
            map.Secondary(SecondaryMap.Beasts).Get(list, center + secondaryStart, secondarySize, Core.Ksid.HasInventory, tag);
            map.Secondary(SecondaryMap.Navigation).Get(list, center + secondaryStart, secondarySize, Core.Ksid.HasInventory, tag);

            SortThem(list, center);
            list.Return();
        }

        private void SortThem(List<Placeable> list, Vector2 center)
        {
            Placeable @base = null, p1 = null, p2 = null;

            foreach (var candidate in list)
            {
                if (candidate != placeableSibling)
                {
                    var dist = (candidate.Center - center).sqrMagnitude;
                    if (dist <= maximumSqrDistance)
                    {
                        if (!TryImprove(candidate, ref @base, ref linkArrNew[0], true, center, dist))
                            if (!TryImprove(candidate, ref p1, ref linkArrNew[1], false, center, dist))
                                TryImprove(candidate, ref p2, ref linkArrNew[2], false, center, dist);
                    }
                }
            }

            hasInventory.Inventory.TryUpdateLinks(linkArrNew, linkArrOld);
            linkArrNew.AsSpan().Clear();
            linkArrOld.AsSpan().Clear();
        }

        private bool TryImprove(Placeable candidate, ref Placeable outP, ref Inventory outI, bool isBase, Vector2 center, float dist)
        {
            if (outP == null || (outP.Center - center).sqrMagnitude > dist)
            {
                var inv2 = candidate.GetComponent<IHasInventory>().Inventory;
                if (inv2 && isBase == (inv2.Type == InventoryType.Base))
                {
                    outP = candidate;
                    outI = inv2;
                    return true;
                }
            }

            return false;
        }

        void IHasAfterMapPlaced.AfterMapPlaced(Map.Map map, Placeable placeableSibling)
        {
            this.map = map;
            this.placeableSibling = placeableSibling;
            Game.Instance.ActivateObject(this);
        }

        public void Cleanup()
        {
            Game.Instance.DeactivateObject(this);
        }
    }
}
