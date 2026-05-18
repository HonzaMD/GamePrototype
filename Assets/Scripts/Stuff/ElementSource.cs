using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Map;
using Assets.Scripts.Map.CellSims;
using Assets.Scripts.Utils;
using System;
using UnityEngine;

namespace Assets.Scripts.Stuff
{
    [RequireComponent(typeof(PlaceableSibling))]
    public class ElementSource : ElementMedium, IActiveObject1Sec, IHasAfterMapPlaced, IHasCleanup
    {
        public void GameUpdate1Sec()
        {
            var placeable =  GetComponent<Placeable>();
            var map = placeable.GetMap();
            Vector2Int cellPos = map.WorldToCell(placeable.Center);
            if ((map.CellSim.GetMaterial(cellPos) & CellSimWorld.DirtMask) != 0 && map.CellSim.GetElement(cellPos) < 5)
                map.CellSim.AddElement(cellPos, 1);
        }

        void IHasAfterMapPlaced.AfterMapPlaced(Map.Map map, Placeable placeableSibling, bool goesFromInventory)
        {
            Game.Instance.ActivateObject(this);
        }

        public new void Cleanup(bool goesToInventory)
        {
            Game.Instance.DeactivateObject(this);
            base.Cleanup(goesToInventory);
        }
    }
}
