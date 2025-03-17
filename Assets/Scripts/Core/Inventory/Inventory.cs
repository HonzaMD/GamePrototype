using Assets.Scripts.Bases;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Core.Inventory
{
    public class Inventory : Label
    {
        public struct Slot
        {
            public Label Prototype;
            public Label Obj;
            public readonly Label Key => Prototype ?? Obj;
            public int Count;
            public float Mass;
            public int DesiredCount;
            public bool IsActivated;
            public int Index;
            public readonly int CountInside => IsActivated ? Count - 1 : Count;
        }

        public InventoryType Type { get; private set; }
        public string Name { get; private set; }
        public Sprite Icon { get; private set; }

        private readonly Slot[] quickAccess = new Slot[10];
        private readonly SpanList<Slot> slots = new();
        private readonly List<Inventory> links = new();
        private readonly Dictionary<Label, int> keyToSlot = new();
        private float mass;
        private Slot dummySlot;
        private Label activeObj;
        private int activeSlot;
        private bool isAlive;

        private InventoryVisualizer visualizer;
        private int visualizerRow;
        private Hud quickSlotsHud;

        public float Mass => mass;
        public Label ActiveObj => activeObj;
        public Connectable ActiveObjHandle;
        public Label LastKey { get; private set; }

        public override Placeable PlaceableC => throw new NotSupportedException();
        public override Label Prototype => Game.Instance.PrefabsStore.Inventory;
        public override Ksid KsidGet => Ksid.Unknown;
        public override bool IsAlive => isAlive;

        public List<Inventory> Links => links;

        private void Awake()
        {
            ActiveObjHandle.Init(Disconnect);
        }

        private Transform Disconnect()
        {
            if (ActiveObj)
                RemoveObjActive();
            return transform;
        }

        public Inventory()
        {
            for (int f = 0; f < quickAccess.Length; f++)
            {
                quickAccess[f].Index = f - quickAccess.Length;
            }
        }

        public override void Init(Map.Map map)
        {
            isAlive = true;
            links.Add(this);
        }

        public void SetupIdentity(string name, InventoryType type, Sprite icon)
        {
            Name = name;
            Type = type;
            Icon = icon;
        }

        public void Clear()
        {
            RemoveObjActive();
            ClearSlots(quickAccess.AsSpan());
            quickAccess.AsSpan().Clear();
            ClearSlots(slots.AsSpan());
            slots.Clear();

            links.Clear();
            keyToSlot.Clear();
            mass = 0f;
            activeObj = null;
            activeSlot = 0;
            isAlive = false;
        }

        private void ClearSlots(Span<Slot> slots)
        {
            for (int f = 0; f < slots.Length; f++)
            {
                var obj = slots[f].Obj;
                if (obj != null && obj != activeObj)
                {
                    obj.Kill();
                }
            }
        }

        public void Store(Label obj)
        {
            if (obj.KsidGet.IsChildOf(Ksid.InventoryAsObj))
            {
                obj.InventoryPush(this);
                StoreObj(obj);
            }
            else
            {
                var prototype = obj.Prototype;
                obj.Kill();
                StoreProto(prototype);
            }
        }

        internal void StoreAsActive(Label obj)
        {
            if (activeObj != null)
                throw new InvalidOperationException("Nemuzu aktivovat, kdyz je neco jineho aktivni");
            if (obj.KsidGet.IsChildOf(Ksid.InventoryAsObj))
            {
                activeSlot = StoreObj(obj);
            }
            else
            {
                var prototype = obj.Prototype;
                activeSlot = StoreProto2(prototype);
            }

            ActivateObj(obj, activeSlot);
        }

        private void ActivateObj(Label obj, int slotNum)
        {
            activeSlot = slotNum;
            activeObj = obj;
            ref var slot = ref GetSlot(slotNum);
            LastKey = slot.Key;
            mass -= slot.Mass;
            slot.IsActivated = true;
            ActiveObjHandle.ConnectTo(obj, ConnectableType.OwnedByInventory);
            HudUpdate(ref slot);
        }

        private void DeactivateObj(ref Slot slot)
        {
            slot.IsActivated = false;
            mass += slot.Mass;
            activeSlot = 0;
            activeObj = null;
            ActiveObjHandle.Disconnect();
            HudUpdate(ref slot);
        }


        public void StoreProto(Label prototype, int count = 1)
        {
            foreach (var inv in links)
            {
                if (count == 0)
                    return;
                int storeHere = inv.DesiredCount(prototype, ref count);
                if (storeHere > 0)
                    inv.StoreProto2(prototype, storeHere);
            }

            if (count > 0)
            {
                foreach (var inv in links)
                {
                    if (inv.Type == InventoryType.Base)
                    {
                        inv.StoreProto2(prototype, count);
                        return;
                    }
                }

                StoreProto2(prototype, count);
            }
        }

        public void SetQuickSlot(int newSlot, Label key)
        {
            if (key == null || newSlot >= 0)
                return;

            if (key.Prototype == key)
            {
                SetQuickSlot(newSlot, StoreProto2(key, 0));
            }
            else
            {
                ref var slot = ref FindSlot(key);
                if (slot.Key == key)
                    SetQuickSlot(newSlot, slot.Index);
            }
        }

        private void SetQuickSlot(int newSlot, int oldSlot)
        {
            if (newSlot == oldSlot)
            {
                newSlot = AddSlot().Index; // zrusi quick slot
            }

            ref var sN = ref GetSlot(newSlot);
            ref var sO = ref GetSlot(oldSlot);
            (sN, sO) = (sO, sN);
            FixIndex(ref sN, newSlot);
            FixIndex(ref sO, oldSlot);
            TryFreeSlot(ref sN);
            TryFreeSlot(ref sO);
            QuickSlotsUpdate(ref sN);
            QuickSlotsUpdate(ref sO);
        }

        private int StoreObj(Label obj)
        {
            float m = obj.GetMass();
            mass += m;
            int index = slots.Count;
            slots.Add(new Slot() { Count = 1, Mass = m, Obj = obj, Index = index });
            keyToSlot.Add(obj, index);
            HudUpdate(ref slots[^1]);
            return index;
        }

        public Label ActivateObj(int slotNum, Transform parent, Vector3 pos, Map.Map map)
        {
            if (slotNum == activeSlot && activeObj != null)
                return activeObj;
            if (activeObj != null)
                throw new InvalidOperationException("Nemuzu aktivovat, kdyz je neco jineho aktivni");
            ref var slot = ref GetSlot(slotNum);
            if (slot.Count == 0 && slot.Key != null)
                TryRefill(ref slot, Math.Max(slot.DesiredCount, 1), links);
            if (slot.Count == 0)
                return null;
            Label newActiveObj;
            if (slot.Obj == null)
            {
                newActiveObj = slot.Prototype.Create(parent, pos, map);
            }
            else
            {
                slot.Obj.InventoryPop(parent, pos, map);
                newActiveObj = slot.Obj;
            }

            ActivateObj(newActiveObj, slotNum);
            return activeObj;
        }

        public void ReturnActiveObj()
        {
            if (activeObj != null)
            {
                if (activeObj.CanBeInInventory(this))
                {
                    ReturnObj(activeObj);
                }
                else
                {
                    RemoveObjActive();
                }
            }
        }

        private void ReturnObj(Label obj)
        {
            Debug.Assert(activeObj == obj, "Cekam ze inventoryObj == label");
            ref var slot = ref GetSlot(activeSlot);
            bool killIt = false;
            if (slot.Obj == null)
            {
                killIt = true;
            }
            else
            {
                slot.Mass = obj.GetMass();
                obj.InventoryPush(this);
            }
            DeactivateObj(ref slot);
            if (killIt)
                obj.Kill();
        }

        internal void RemoveObjActive()
        {
            if (activeObj != null)
                RemoveObj(activeSlot, 1);
        }

        // pokud nejsou objekty aktivovani, tak budou killnuty
        public void RemoveObj(int slotNum, int count)
        {
            ref var slot = ref GetSlot(slotNum);
            count = Math.Min(slot.Count, count);
            if (count == 0)
                return;
            Label oldKey = slot.Key;
            if (slotNum == activeSlot && activeObj != null)
            {
                DeactivateObj(ref slot);
            }
            else if (slot.Obj != null)
            {
                slot.Obj.Kill();
            }
            slot.Obj = null;
            slot.Count -= count;
            mass -= slot.Mass * count;
            TryFreeAndNotify(ref slot, oldKey);
        }

        public void Balance(Label key)
        {
            foreach (var inv in links)
            {
                inv.TryRefill(key, links);
            }
        }

        private void TryRefill(Label key, List<Inventory> links)
        {
            ref var slot = ref FindSlot(key);
            if (slot.Key == key)
                TryRefill(ref slot, slot.DesiredCount, links);
        }

        private void TryRefill(ref Slot slot, int desiredCount, List<Inventory> links)
        {
            int toFill = desiredCount - slot.Count;
            int removed = 0;
            foreach (var link in links)
            {
                if (toFill <= 0)
                    break;
                if (link != this)
                    removed += link.RemoveUnneeded(slot.Key, ref toFill);
            }
            MoveItems(ref slot, removed);
        }

        private int RemoveUnneeded(Label key, ref int requestedCount)
        {
            ref var slot = ref FindSlot(key);
            if (slot.Key == null)
                return 0;
            int count = Math.Min(slot.CountInside - slot.DesiredCount, requestedCount);
            if (count <= 0)
                return 0;
            MoveItems(ref slot, -count);
            requestedCount -= count;
            return count;
        }

        private void MoveItems(ref Slot slot, int count)
        {
            if (count == 0)
                return;
            Label oldKey = slot.Key;
            slot.Count += count;
            mass += slot.Mass * count;
            TryFreeAndNotify(ref slot, oldKey);
        }

        private void TryFreeAndNotify(ref Slot slot, Label oldKey)
        {
            if (TryFreeSlot(ref slot))
            {
                if (oldKey != null && visualizer != null)
                    visualizer.HideItem(oldKey, visualizerRow);
                QuickSlotsUpdate(ref slot);
            }
            else
            {
                HudUpdate(ref slot);
            }
        }

        private bool TryFreeSlot(ref Slot slot)
        {
            if (slot.DesiredCount > 0 || slot.Count > 0)
                return false;
            if (slot.IsActivated)
                throw new InvalidOperationException("Divnost, necekal jsem ze bude aktivovanej");
            if (slot.Index < 0)
            {
                if (slot.Obj != null)
                {
                    keyToSlot.Remove(slot.Obj);
                    slot.Obj = null;
                    slot.Prototype = null;
                    slot.Mass = 0;
                    return true;
                }
                return false;
            }
            else
            {
                if (slot.Key != null)
                    keyToSlot.Remove(slot.Key);
                int index = slot.Index;
                slots.RemoveAt(index);
                FixIndex(ref slot, index);
                return true;
            }
        }

        private void FixIndex(ref Slot slot, int index)
        {
            slot.Index = index;
            if (slot.Key != null && keyToSlot.ContainsKey(slot.Key))
            {
                keyToSlot[slot.Key] = index;
            }
            if (slot.IsActivated)
            {
                activeSlot = index;
            }
        }


        private int StoreProto2(Label prototype, int count = 1)
        {
            ref var slot = ref FindSlot(prototype);
            if (slot.Prototype == null)
            {
                slot = ref AddSlot();
                slot.Prototype = prototype;
                slot.Mass = prototype.GetMass();
                keyToSlot.Add(prototype, slot.Index);
            }
            MoveItems(ref slot, count);
            return slot.Index;
        }

        private ref Slot AddSlot()
        {
            slots.Add(new() { Index = slots.Count });
            return ref slots[^1];
        }

        private int DesiredCount(Label prototype, ref int toStore)
        {
            ref var slot = ref FindSlot(prototype);
            int storeHere = Math.Min(Math.Max(0, slot.DesiredCount - slot.Count), toStore);
            toStore -= storeHere;
            return storeHere;
        }

        public ref Slot FindSlot(Label key)
        {
            if (key != null && keyToSlot.TryGetValue(key, out var slot))
            {
                return ref GetSlot(slot);
            }
            else
            {
                return ref dummySlot;
            }
        }

        public bool TryGetSlot(Label key, out int slot)
        {
            if (key == null)
            {
                slot = 0;
                return false;
            }
            return keyToSlot.TryGetValue(key, out slot);
        }

        private ref Slot GetSlot(int slot)
        {
            if (slot < 0)
                return ref quickAccess[slot + quickAccess.Length];
            else
                return ref slots[slot];
        }

        public override void Cleanup()
        {
            Clear();
            base.Cleanup();
        }

        internal bool HasObj(int slot) => GetSlot(slot).Count > 0;

        internal void ShowInHud(InventoryVisualizer inventoryVisualizer, int row)
        {
            visualizer = inventoryVisualizer;
            visualizerRow = row;

            HudUpdate(quickAccess.AsSpan());
            HudUpdate(slots.AsSpan());
        }

        internal void StopShowInHud() => visualizer = null;

        private void HudUpdate(Span<Slot> slots)
        {
            foreach (ref var slot in slots)
            {
                if (slot.Key != null)
                    visualizer.UpdateItem(slot.Key, slot.CountInside, slot.DesiredCount, visualizerRow);
            }
        }

        private void HudUpdate(ref Slot slot)
        {
            if (visualizer != null && slot.Key != null)
                visualizer.UpdateItem(slot.Key, slot.CountInside, slot.DesiredCount, visualizerRow);
            QuickSlotsUpdate(ref slot);
        }

        private void QuickSlotsUpdate(ref Slot slot)
        {
            if (quickSlotsHud != null && slot.Index < 0)
                quickSlotsHud.UpdateQuickSlot(slot.Index, slot.Key, slot.CountInside);
        }

        internal void ShowInQuickSlots()
        {
            quickSlotsHud = Game.Instance.Hud;
            foreach (ref var slot in quickAccess.AsSpan())
            {
                quickSlotsHud.UpdateQuickSlot(slot.Index, slot.Key, slot.CountInside);
            }
        }

        internal void DisconnectQuickSlots()
        {
            quickSlotsHud = null;
        }


        internal void TryPaste(Inventory srcInventory, Label key)
        {
            int moveCount = CanPaste(srcInventory, key);
            if (moveCount == 0)
                return;

            ref var srcSlot = ref srcInventory.FindSlot(key);
            ref var destSlot = ref FindSlot(key);
            if (destSlot.Key != key)
            {
                destSlot = ref GetSlot(StoreProto2(key, 0)); // TODO poresit OBJ
            }
            srcInventory.MoveItems(ref srcSlot, -moveCount);
            MoveItems(ref destSlot, moveCount);
        }

        internal int CanPaste(Inventory srcInventory, Label key)
        {
            if (srcInventory == null || srcInventory == this)
                return 0;

            ref var srcSlot = ref srcInventory.FindSlot(key);
            if (srcSlot.CountInside <= 0)
                return 0;
            int freeCount = Mathf.Max(0, srcSlot.CountInside - srcSlot.DesiredCount);

            ref var destSlot = ref FindSlot(key);
            if (destSlot.Key != key)
            {
                return freeCount;
            }

            if (destSlot.DesiredCount > destSlot.Count)
            {
                return Mathf.Min(srcSlot.CountInside, destSlot.DesiredCount - destSlot.Count);
            }
            else
            {
                return freeCount;
            }
        }

        internal void TryUpdateLinks(Inventory[] linkArrNew, Inventory[] linkArrOld)
        {
            ExpandLinks(linkArrOld);
            if (LinksChanged(linkArrNew, linkArrOld))
            {
                CompactLinks(linkArrNew);
                if (visualizer != null && visualizerRow == 0)
                    visualizer.LinksChanged();
                if (links.Count > 1)
                    BalanceAll();
            }
        }

        private void BalanceAll()
        {
            BalanceAll(quickAccess.AsSpan());
            BalanceAll(slots.AsSpan());
        }

        private void BalanceAll(Span<Slot> slots)
        {
            foreach (ref var slot in slots)
            {              
                if (slot.Key != null)
                    Balance(slot.Key);
            }
        }

        private void CompactLinks(Inventory[] linkArrNew)
        {
            links.Clear();
            links.Add(this);
            foreach (var link in linkArrNew)
            {
                if (link != null)
                    links.Add(link);
            }
        }

        private void ExpandLinks(Inventory[] linkArrOld)
        {
            int p = 0;
            for (int i = 1; i < links.Count; i++, p++)
            {
                if (i == 1 && links[i].Type != InventoryType.Base)
                    p++;

                linkArrOld[p] = links[i];
            }
        }

        private bool LinksChanged(Inventory[] linkArrNew, Inventory[] linkArrOld)
        {
            if ((linkArrNew[1] != null && linkArrNew[1] == linkArrOld[2]) || (linkArrNew[2] != null && linkArrNew[2] == linkArrOld[1]))
                (linkArrNew[1], linkArrNew[2]) = (linkArrNew[2], linkArrNew[1]);

            for (int i = 0; i < linkArrNew.Length; i++)
            {
                if (linkArrNew[i] != linkArrOld[i])
                    return true;
            }

            return false;
        }
    }

    public enum InventoryType
    {
        Unknown,
        Character,
        Base,
        Chest,
    }
}
