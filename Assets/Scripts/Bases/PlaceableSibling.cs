using Assets.Scripts.Core.Inventory;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Bases
{
    public interface IHasCleanup
    {
        void Cleanup(bool goesToInventory);
    }

    public interface IHasAfterMapPlaced
    {
        void AfterMapPlaced(Map.Map map, Placeable placeableSibling, bool goesFromInventory);
    }

    public interface IHasInventory
    {
        Inventory Inventory { get;}
    }

    public class PlaceableSibling : Placeable
    {
        private bool isInInventory;

        public override bool IsInInventory => isInInventory;

        public override void Cleanup(bool goesToInventory)
        {
            base.Cleanup(goesToInventory);
            var cleanups = ListPool<IHasCleanup>.Rent();
            GetComponents(cleanups);
            foreach (var c in cleanups)
                c.Cleanup(goesToInventory);
            cleanups.Return();
            var rb = GetComponent<Rigidbody>();
            if (rb)
                rb.Cleanup();
            isInInventory = goesToInventory;
        }

        protected override void AfterMapPlaced(Map.Map map, bool goesFromInventory)
        {
            base.AfterMapPlaced(map, goesFromInventory);
            var components = ListPool<IHasAfterMapPlaced>.Rent();
            GetComponents(components);
            foreach (var c in components)
                c.AfterMapPlaced(map, this, goesFromInventory);
            components.Return();
        }

        public override void InventoryPop(Transform parent, Vector3 pos, Map.Map map)
        {
            transform.parent = parent;
            transform.position = pos;
            gameObject.SetActive(true);
            isInInventory = false;
            PlaceToMap(map, true);
        }

        public override void InventoryPush(Inventory inventory)
        {
            if (TryGetParentLabel(out var pl))
                pl.DetachKilledChild(this);
            RecursiveCleanupToInventory();
            gameObject.SetActive(false);
            transform.parent = Game.Instance.InventoryRoot;
            isInInventory = true;
        }
    }
}
