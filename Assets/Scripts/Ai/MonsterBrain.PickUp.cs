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
        private const float MaxPickUpSpeed = 1f;    // [m/s] relativni rychlost predmetu vuci prisere

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
        // Rychly predmet (napr. prave hozeny) ruka nechyta - tazeni proti jeho hybnosti priseru zranuje.
        protected override bool IsPickupAllowed(Label p) => p.KsidGet.IsChildOfOrEq(pickUpKsid)
            && (p.Velocity - body.linearVelocity).sqrMagnitude < MaxPickUpSpeed * MaxPickUpSpeed;

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

        public override Label InventoryGet() => inventory != null ? inventory.ActiveObj : null;
        public override bool IsInventoryActive => inventory != null && inventory.ActiveObj != null;
        protected override void InventoryReturnRaw() => inventory.ReturnActiveObj();
        protected override void InventoryDropRaw() => inventory.RemoveObjActive();

        // Drzi v ruce predmet vytazeny z inventare (DrawItem uz uspel).
        public bool HoldsDrawnItem => IsInventoryActive && GetHoldObject() == inventory.ActiveObj;

        // Prikaz na JEDEN krok (pravidlo ho vola v kazdem Tick): vytahni predmet typu ksid z inventare
        // do ruky a drz ho. Kdyz ho pravidlo prestane volat, predmet se vrati do inventare sam
        // (GameFixedUpdate / OnLimbDetached). False = neni co vytahnout.
        public bool DrawItem(Ksid ksid)
        {
            if (IsInventoryActive && !inventory.ActiveObj.KsidGet.IsChildOfOrEq(ksid))
                InventoryReturn();      // pusti i ruku, ktera ho drzi
            if (!IsInventoryActive)
            {
                if (inventory == null || !inventory.TryFindSlotKsid(ksid, out int slot, out _))
                    return false;
                // holdTarget motor nastavi jen pri prvnim uchopeni a sam ho nemeni - bez toho by
                // prisera drzela vsechno na strane z prvniho hodu
                holdTarget = HoldOffset(targetPointer.x);
                // v rovine prisery - ArmSphere double-cell prisery je mezi rovinami, odkud by predmet neslo prendat
                var spawnPos = (ArmSphere.transform.position.XY() + holdTarget).AddZ(PlaneZ(placeable.CellZ));
                if (inventory.ActivateObj(slot, placeable.LevelGroup, spawnPos, map) == null)
                    return false;
            }
            desiredHold = true;
            return true;
        }

        // Kde bude predmet v ruce vuci ArmSphere - strana podle toho, kam se prisera diva.
        private Vector2 HoldOffset(float towardX)
        {
            float dir = towardX >= Center.x ? 1 : -1;
            return new Vector2(Settings.HoldPosition.x * dir, Settings.HoldPosition.y);
        }

        private Vector3 HoldPoint(float towardX) => ArmSphere.transform.position + HoldOffset(towardX).AddZ(0);

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
