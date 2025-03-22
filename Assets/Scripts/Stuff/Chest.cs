using Assets.Scripts.Bases;
using Assets.Scripts.Core.Inventory;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Playables;

namespace Assets.Scripts.Stuff
{
    [RequireComponent(typeof(PlaceableSibling), typeof(Rigidbody))]
    public class Chest : MonoBehaviour, IHasInventory, IHasAfterMapPlaced, IHasCleanup
    {
        private Inventory inventory;

        public Placeable[] InitialContent;
        public int[] InitialContentCount;

        public Inventory Inventory => inventory;

        void IHasAfterMapPlaced.AfterMapPlaced(Map.Map map, Placeable placeableSibling, bool goesFromInventory)
        {
            if (inventory == null)
            {
                inventory = Game.Instance.PrefabsStore.Inventory.Create(Game.Instance.InventoryRoot, Vector3.zero, null);
                inventory.SetupIdentity("Chest", InventoryType.Chest, GetComponent<Placeable>().Settings.Icon);
                if (InitialContent != null)
                {
                    for (int i = 0; i < InitialContent.Length; i++)
                    {
                        var p = InitialContent[i];
                        if (p.Prototype == p)
                        {
                            int count = i < InitialContentCount?.Length && InitialContentCount[i] > 0 ? InitialContentCount[i] : 1;
                            inventory.StoreProto(p, count);
                        }
                        else
                        {
                            inventory.Store(p);
                        }
                    }
                }

                InitialContent = null;
                InitialContentCount = null;
            }
        }

        void IHasCleanup.Cleanup(bool goesToInventory)
        {
            if (!goesToInventory)
            {
                inventory.Kill();
                inventory = null;
            }
        }
    }
}
