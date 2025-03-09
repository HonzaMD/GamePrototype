using Assets.Scripts.Core.Inventory;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Bases
{
    public interface IHasCleanup
    {
        void Cleanup();
    }

    public interface IHasAfterMapPlaced
    {
        void AfterMapPlaced(Map.Map map, Placeable placeableSibling);
    }

    public interface IHasInventory
    {
        Inventory Inventory { get;}
    }

    public class PlaceableSibling : Placeable
    {
        public override void Cleanup()
        {
            base.Cleanup();
            var cleanups = ListPool<IHasCleanup>.Rent();
            GetComponents(cleanups);
            foreach (var c in cleanups)
                c.Cleanup();
            cleanups.Return();
        }

        protected override void AfterMapPlaced(Map.Map map)
        {
            base.AfterMapPlaced(map);
            var components = ListPool<IHasAfterMapPlaced>.Rent();
            GetComponents(components);
            foreach (var c in components)
                c.AfterMapPlaced(map, this);
            components.Return();
        }
    }
}
