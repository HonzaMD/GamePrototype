﻿using Assets.Scripts.Bases;
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
        private struct Slot
        {
            public Label Prototype;
            public Label Obj;
            public int Count;
            public float Mass;
            public int DesiredCount;
            public bool IsActivated;
            public int Index;
            public int CountInside => IsActivated ? Count - 1 : Count;
        }

        private struct Link
        {
            public Inventory Inventory;
            public bool AllowGet;
            public bool AllowStore;
        }

        private readonly Slot[] quickAccess = new Slot[10];
        private readonly SpanList<Slot> slots = new();
        private readonly List<Link> links = new();
        private readonly Dictionary<Label, int> protoToSlot = new();
        private float mass;
        private Slot dummySlot;
        private Label activeObj;
        private int activeSlot;
        private bool isAlive;

        public float Mass => mass;
        public Label ActiveObj => activeObj;
        public Connectable ActiveObjHandle;
        public int LastSlot { get; private set; }

        public override Placeable PlaceableC => throw new NotSupportedException();
        public override Label Prototype => Game.Instance.PrefabsStore.Inventory;
        public override Ksid KsidGet => Ksid.Unknown;
        public override bool IsAlive => isAlive;

        private void Awake()
        {
            ActiveObjHandle.Init(Disconnect);
        }

        private Transform Disconnect()
        {
            if (ActiveObj)
                DropObjActive();
            return transform;
        }

        public Inventory()
        {
            for (int f = 0; f < quickAccess.Length; f++)
            {
                quickAccess[f].Index = f - quickAccess.Length;
            }
        }

        public override void Init(Map.Map map) => isAlive = true;

        public void Clear()
        {
            DropObjActive();
            ClearSlots(quickAccess.AsSpan());
            quickAccess.AsSpan().Clear();
            ClearSlots(slots.AsSpan());
            slots.Clear();

            links.Clear();
            protoToSlot.Clear();
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
            LastSlot = slotNum;
            ref var slot = ref GetSlot(slotNum);
            mass -= slot.Mass;
            slot.IsActivated = true;
            ActiveObjHandle.ConnectTo(obj, ConnectableType.OwnedByInventory);
        }

        private void DeactivateObj(ref Slot slot)
        {
            slot.IsActivated = false;
            mass += slot.Mass;
            activeSlot = 0;
            activeObj = null;
            ActiveObjHandle.Disconnect();
        }


        public void StoreProto(Label prototype, int count = 1)
        {
            if (IsDesired(prototype)) 
            { 
                StoreProto2(prototype, count);
            }
            else if (FindOtherInventoryStore(out Inventory inv))
            {
                inv.StoreProto(prototype, count);
            }
            else
            {
                StoreProto2(prototype, count);
            }
        }

        public void SetQuickSlot(int newSlot, Label prototype)
        {
            SetQuickSlot(newSlot, StoreProto2(prototype, 0));
        }

        private void SetQuickSlot(int newSlot, int oldSlot)
        {
            ref var sN = ref GetSlot(newSlot);
            ref var sO = ref GetSlot(oldSlot);
            (sN, sO) = (sO, sN);
            FixIndex(ref sN, newSlot);
            FixIndex(ref sO, oldSlot);
            TryFreeSlot(ref sN);
            TryFreeSlot(ref sO);
        }

        private int StoreObj(Label obj)
        {
            float m = obj.GetMass();
            mass += m;
            int index = slots.Count;
            slots.Add(new Slot() { Count = 1, Mass = m, Obj = obj, Index = index });
            return index;
        }

        public Label ActivateObj(int slotNum, Transform parent, Vector3 pos, Map.Map map)
        {
            if (slotNum == activeSlot && activeObj != null)
                return activeObj;
            if (activeObj != null)
                throw new InvalidOperationException("Nemuzu aktivovat, kdyz je neco jineho aktivni");
            ref var slot = ref GetSlot(slotNum);
            if (slot.Count == 0)
                BalanceSlot(ref slot, Math.Max(slot.DesiredCount, 1));
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
                    DeactivateObj(activeObj);
                }
                else
                {
                    DropObjActive();
                }
            }
        }

        public void DeactivateObj(Label obj)
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

        internal void DropObjActive()
        {
            if (activeObj != null)
                DropObj(activeSlot, 1);
        }

        // pokud nejsou objekty aktivovani, tak budou killnuty
        public void DropObj(int slotNum, int count)
        {
            ref var slot = ref GetSlot(slotNum);
            count = Math.Min(slot.Count, count);
            if (count == 0) 
                return;
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
            TryFreeSlot(ref slot);
        }


        private void BalanceSlot(ref Slot slot, int desiredCount)
        {
            if (slot.Prototype == null || slot.Obj != null)
                return;
            int delta = desiredCount - slot.Count;
            foreach (var link in links)
            {
                if (delta == 0)
                    return;
                if (delta > 0 && link.AllowGet)
                {
                    link.Inventory.GetProto(slot.Prototype, ref delta);
                    slot.Count = desiredCount - delta;
                }
                if (delta < 0 && link.AllowStore)
                {
                    link.Inventory.StoreProto2(slot.Prototype, -delta);
                    slot.Count = desiredCount;
                    return;
                }
            }
        }

        private void GetProto(Label prototype, ref int requestedCount)
        {
            ref var slot = ref FindSlot(prototype);
            if (slot.Prototype == null)
                return;
            int count = Math.Min(slot.CountInside, requestedCount);
            if (count == 0)
                return;
            slot.Count -= count;
            mass -= slot.Mass * count;
            requestedCount -= count;
            TryFreeSlot(ref slot);
        }

        private void TryFreeSlot(ref Slot slot)
        {
            if (slot.DesiredCount > 0 || slot.Count > 0)
                return;
            if (slot.IsActivated)
                throw new InvalidOperationException("Divnost, necekal jsem ze bude aktivovanej");
            if (slot.Index < 0)
            {
                if (slot.Obj != null)
                {
                    slot.Obj = null;
                    slot.Prototype = null;
                    slot.Mass = 0;
                }
            }
            else
            {
                if (slot.Prototype != null)
                    protoToSlot.Remove(slot.Prototype);
                int index = slot.Index;
                slots.RemoveAt(index);
                FixIndex(ref slot, index);
            }
        }

        private void FixIndex(ref Slot slot, int index)
        {
            slot.Index = index;
            if (slot.Prototype != null && protoToSlot.ContainsKey(slot.Prototype))
            {
                protoToSlot[slot.Prototype] = index;
            }
            if (slot.IsActivated)
            {
                activeSlot = index;
            }
        }

        private bool FindOtherInventoryStore(out Inventory inv)
        {
            foreach (var link in links)
            {
                if (link.AllowStore)
                {
                    inv = link.Inventory;
                    return true;
                }
            }
            inv = null;
            return false;
        }

        private int StoreProto2(Label prototype, int count = 1)
        {
            ref var slot = ref FindSlot(prototype);
            if (slot.Prototype == null)
            {
                slot = ref AddSlot();
                slot.Prototype = prototype;
                slot.Mass = prototype.GetMass();
                protoToSlot.Add(prototype, slot.Index);
            }
            slot.Count += count;
            mass += slot.Mass * count;
            return slot.Index;
        }

        private ref Slot AddSlot()
        {
            slots.Add(new() { Index = slots.Count});
            return ref slots[^1];
        }

        private bool IsDesired(Label prototype)
        {
            ref var slot = ref FindSlot(prototype);
            return slot.Count < slot.DesiredCount;
        }

        private ref Slot FindSlot(Label prototype)
        {
            if (protoToSlot.TryGetValue(prototype, out var slot))
            {
                return ref GetSlot(slot);
            }
            else
            {
                return ref dummySlot;
            }
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
    }
}
