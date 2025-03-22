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
            public Label Key;
            public int Count;
            public float Mass;
            public int DesiredCount;
            public bool IsActivated;
            public bool IsLiveObj;
            public int Index;
            public readonly int CountInside => IsActivated ? Count - 1 : Count;
            public Label LiveObj => CountInside > 0 && IsLiveObj ? Key : null;
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
                var obj = slots[f].LiveObj;
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
                StoreProto(obj, 1, true);
            }
            else
            {
                var key = obj.Prototype;
                obj.Kill();
                StoreProto(key, 1, false);
            }
        }

        internal void StoreAsActive(Label obj)
        {
            if (activeObj != null)
                throw new InvalidOperationException("Nemuzu aktivovat, kdyz je neco jineho aktivni");
            if (obj.KsidGet.IsChildOf(Ksid.InventoryAsObj))
            {
                activeSlot = StoreProto2(obj, 1);
            }
            else
            {
                var prototype = obj.Prototype;
                activeSlot = StoreProto2(prototype, 1);
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


        public void StoreProto(Label key, int count = 1, bool isLiveObj = false)
        {
            foreach (var inv in links)
            {
                if (count == 0)
                    return;
                int storeHere = inv.DesiredCount(key, ref count);
                if (storeHere > 0)
                    inv.StoreProto2(key, storeHere);
            }

            if (count > 0)
            {
                if (!isLiveObj)
                {
                    foreach (var inv in links)
                    {
                        if (inv.Type == InventoryType.Base)
                        {
                            inv.StoreProto2(key, count);
                            return;
                        }
                    }
                }

                StoreProto2(key, count);
            }
        }

        public void SetQuickSlot(int newSlot, Label key)
        {
            if (key == null || newSlot >= 0)
                return;

            SetQuickSlot(newSlot, FindOrCreateSlot(key).Index);
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
            HudUpdate(ref sN);
            QuickSlotsUpdate(ref sO);
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
            if (!slot.IsLiveObj)
            {
                newActiveObj = slot.Key.Create(parent, pos, map);
            }
            else
            {
                slot.Key.InventoryPop(parent, pos, map);
                newActiveObj = slot.Key;
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
            if (slot.IsLiveObj)
                slot.Mass = obj.GetMass();
            DeactivateObj(ref slot);
            if (!slot.IsLiveObj)
            {
                obj.Kill();
            }
            else
            {               
                obj.InventoryPush(this);
            }
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
            if (slotNum == activeSlot && activeObj != null)
            {
                DeactivateObj(ref slot);
            }
            else if (slot.IsLiveObj)
            {
                slot.Key.Kill();
            }
            slot.Count -= count;
            mass -= slot.Mass * count;
            TryFreeAndNotify(ref slot);
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
            slot.Count += count;
            mass += slot.Mass * count;
            TryFreeAndNotify(ref slot);
        }

        private void TryFreeAndNotify(ref Slot slot)
        {
            if (TryFreeSlot(ref slot))
            {
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
                return false;
            }
            else
            {
                var oldKey = slot.Key;
                if (oldKey != null)
                    keyToSlot.Remove(oldKey);
                int index = slot.Index;
                slots.RemoveAt(index);
                FixIndex(ref slot, index);

                if (oldKey != null && visualizer != null)
                    visualizer.HideItem(oldKey, visualizerRow);
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


        private int StoreProto2(Label key, int count)
        {
            ref var slot = ref FindOrCreateSlot(key);
            if (slot.IsLiveObj)
            {
                slot.Mass = key.GetMass();
            }
            MoveItems(ref slot, count);
            return slot.Index;
        }

        private ref Slot AddSlot()
        {
            slots.Add(new() { Index = slots.Count });
            return ref slots[^1];
        }

        private int DesiredCount(Label key, ref int toStore)
        {
            ref var slot = ref FindSlot(key);
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

        public ref Slot FindOrCreateSlot(Label key)
        {
            if (keyToSlot.TryGetValue(key, out var index))
                return ref GetSlot(index);

            ref var slot = ref AddSlot();
            slot.Key = key;
            slot.Mass = key.GetMass();
            slot.IsLiveObj = key.KsidGet.IsChildOf(Ksid.InventoryAsObj);
            keyToSlot.Add(key, slot.Index);
            return ref slot;
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

        public override void Cleanup(bool goesToInventory)
        {
            Clear();
            base.Cleanup(goesToInventory);
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
            ref var destSlot = ref FindOrCreateSlot(key);
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
            var keysList = ListPool<Label>.Rent();
            foreach (var key in keyToSlot.Keys) 
                keysList.Add(key);
            foreach (var key in keysList)
                Balance(key);
            keysList.Return();
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
