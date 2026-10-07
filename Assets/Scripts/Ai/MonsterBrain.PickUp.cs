using Assets.Scripts.Core;
using Assets.Scripts.Core.Inventory;
using Assets.Scripts.Utils;
using UnityEngine;

namespace Assets.Scripts.Ai
{
    // SBIRANI predmetu do inventare.
    //
    // Cil se predava jako BOD (ukazatel, LookAt) + Ksid, ne Label - stejne jako hrac sbira to, co je
    // pod mysi. Label najde motor az v kroku uchopeni (TryHoldNearItem), dal ho drzi Connectable ruky.
    //
    // Inventar vznika az pri prvnim sebrani (prisery bez koristi nic nestoji) a schvalne NEMA
    // Ksid HasInventory - jinak by si ho hracuv InventorySearcher nalinkoval a auto-refill by
    // z prisery tahal veci. Pri smrti se zabije i s obsahem.
    public abstract partial class MonsterBrain
    {
        private Inventory inventory;
        private Ksid pickUpKsid;

        public Ksid SenseKsid(SenseId id) => AiSettings.ResolvedSenses[(int)id].Ksid;

        public int CountItems(Ksid ksid) => inventory == null ? 0 : inventory.CountKsid(ksid);

        // Saturace: uz ma aspon maxCount kusu toho, co hleda smysl id.
        public bool IsSaturated(SenseId id, int maxCount) => CountItems(SenseKsid(id)) >= maxCount;

        // Prikaz na JEDEN krok (pravidlo ho vola v kazdem Tick): seber predmet typu ksid, ktery lezi
        // na ukazateli (LookAt). Kdyz ruka predmet pusti (moc tezky), po timeoutu ruky to zkusi znovu.
        public void PickUp(Ksid ksid)
        {
            pickUpKsid = ksid;
            desiredPickUp = true;
        }

        protected override Ksid PickupQueryKsid => pickUpKsid;
        protected override bool IsPickupAllowed(Label p) => p.KsidGet.IsChildOfOrEq(pickUpKsid);

        // Vola motor, kdyz ruka v rezimu PickUp dotahne predmet k telu (OnLimbDetached).
        protected override void InventoryPickup(Label label)
        {
            if (!placeable.IsAlive)
                return;     // prisera umira (Cleanup -> DetachAllLimbs) - predmet radsi upusti do sveta

            if (inventory == null)
            {
                inventory = Game.Instance.PrefabsStore.Inventory.Create(Game.Instance.InventoryRoot, Vector3.zero, null);
                inventory.SetupIdentity(name, InventoryType.Monster, placeable.Settings.Icon);
            }
            if (label.CanBeInInventory(inventory))
                inventory.Store(label);
        }

        private void CleanupInventory()
        {
            if (inventory != null)
            {
                inventory.Kill();
                inventory = null;
            }
        }
    }
}
