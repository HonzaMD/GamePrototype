using Assets.Scripts.Core.Inventory;
using Assets.Scripts.Map.CellSims;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

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

    public interface IHasMapPlacedStatic
    {
        void MapPlacedStatic(Map.Map map, Placeable placeableSibling);
    }

    public interface IHasMapRemovedStatic
    {
        void MapRemovedStatic(Map.Map map, Placeable placeableSibling);
    }

    public interface IHasDirtFlagChange
    {
        void DirtFlagChange(Map.Map map, Vector2Int cellPos, MaterialChangeType changeType, Placeable placeableSibling);
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

        protected override void MapPlacedStatic(Map.Map map)
        {
            base.MapPlacedStatic(map);
            var components = ListPool<IHasMapPlacedStatic>.Rent();
            GetComponents(components);
            foreach (var c in components)
                c.MapPlacedStatic(map, this);
            components.Return();
        }

        protected override void MapRemovedStatic(Map.Map map)
        {
            base.MapRemovedStatic(map);
            var components = ListPool<IHasMapRemovedStatic>.Rent();
            GetComponents(components);
            foreach (var c in components)
                c.MapRemovedStatic(map, this);
            components.Return();
        }

        protected override void DirtFlagChange(Map.Map map, Vector2Int cellPos, MaterialChangeType changeType)
        {
            base.DirtFlagChange(map, cellPos, changeType);
            var components = ListPool<IHasDirtFlagChange>.Rent();
            GetComponents(components);
            foreach (var c in components)
                c.DirtFlagChange(map, cellPos, changeType, this);
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
