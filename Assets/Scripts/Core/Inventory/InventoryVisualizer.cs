using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UiLabel = UnityEngine.UIElements.Label;

namespace Assets.Scripts.Core.Inventory
{
    internal class InventoryVisualizer
    {
        private const string InventoryButtonSelected = "InventoryButtonSelected";

        private readonly SpanList<Slot> slots = new();
        private Inventory primaryInventory;
        private readonly Inventory[] inventories = new Inventory[4];
        private readonly Dictionary<Label, int> keyToSlot = new();
        private readonly Dictionary<VisualElement, int> columnToSlot = new();
        private readonly ReqNumManipulator reqNumManipulator = new();
        private readonly ItemDragDropManipulator itemDragDropManipulator = new();
        private readonly ScrollManipulator scrollManipulator = new ScrollManipulator();
        private readonly InvNameFilter invNameFilter;

        private readonly VisualElement inventoryPanel;
        private readonly VisualTreeAsset columnAsset;
        private readonly VisualElement invNamesPanel;
        private readonly VisualElement itemDragElement;
        private readonly ScrollView scrollView;
        private readonly UiLabel itemDragNum;
        private int rowCount, prevRowCount;
        private int selectedSlot = -1;
        private bool dirty;
        public bool Visible { get; set; }

        public Label SelectedKey => selectedSlot != -1 ? slots[selectedSlot].Key : null;

        private struct Slot
        {
            public readonly Label Key;
            public readonly VisualElement Column;
            public readonly int IconOrder;
            public int activeRows;
            public bool Visible;
            
            public void ActivateRow(int row) => activeRows |= (1 << row);
            public void DeactivateRow(int row) => activeRows &= ~(1 << row);
            public bool IsRowActive(int row) => ((1 << row) & activeRows) != 0;

            public Slot(Label key, VisualElement column, int iconOrder)
            {
                Key = key;
                Column = column;
                IconOrder = iconOrder;
                Visible = true;
                activeRows = 0;
            }
        }

        private class InvNameFilter
        {
            private bool AllVisible = true;
            private int activeRows;
            private readonly InventoryVisualizer visualizer;
            private readonly Button allNamesButton;
            private readonly Button[] nameButtons;

            public InvNameFilter(InventoryVisualizer visualizer, Button allNamesButton, VisualElement invNamesPanel)
            {
                this.visualizer = visualizer;
                this.allNamesButton = allNamesButton;
                nameButtons = invNamesPanel.Children().Select(ch => ch.Q<Button>("nameButton")).ToArray();
                for (int i = 0; i < nameButtons.Length; i++)
                {
                    int row = i;
                    nameButtons[i].clicked += () => ToggleRow(row);
                }
                allNamesButton.clicked += AllNamesButton_clicked;
            }

            private void AllNamesButton_clicked()
            {
                if (!AllVisible)
                {
                    AllVisible = true;
                    allNamesButton.AddToClassList(InventoryButtonSelected);

                    for (int row = 0; row < nameButtons.Length; row++)
                    {
                        if (IsRowActive(row))
                        {
                            nameButtons[row].RemoveFromClassList(InventoryButtonSelected);
                        }
                    }

                    activeRows = 0;

                    visualizer.ChangeFilters();
                }
            }

            private void ToggleRow(int row)
            {
                bool wasActive = IsRowActive(row);
                if (AllVisible)
                {
                    AllVisible = false;
                    allNamesButton.RemoveFromClassList(InventoryButtonSelected);
                }

                nameButtons[row].EnableInClassList(InventoryButtonSelected, !wasActive);
                if (wasActive) 
                    DeactivateRow(row);
                else
                    ActivateRow(row);

                if (activeRows == 0)
                {
                    AllVisible = true;
                    allNamesButton.AddToClassList(InventoryButtonSelected);
                }

                visualizer.ChangeFilters();
            }

            public void ClearRow(int row)
            {
                if (IsRowActive(row))
                    ToggleRow(row);
            }

            private void ActivateRow(int row) => activeRows |= (1 << row);
            private void DeactivateRow(int row) => activeRows &= ~(1 << row);
            private bool IsRowActive(int row) => ((1 << row) & activeRows) != 0;

            public bool Test(ref Slot slot)
            {
                return (AllVisible && slot.activeRows != 0) || (activeRows & slot.activeRows) != 0;
            }
        }

        public InventoryVisualizer(VisualElement inventoryPanel, VisualElement inventoryWindow, VisualTreeAsset columnAsset, VisualElement itemDragElement, ScrollView scrollView)
        {
            this.inventoryPanel = inventoryPanel;
            this.columnAsset = columnAsset;
            this.itemDragElement = itemDragElement;
            this.scrollView = scrollView;
            itemDragNum = itemDragElement.Q<UiLabel>("itemDragNum");
            invNamesPanel = inventoryWindow.Q("InvNamesPanel");
            var allNamesButton = inventoryWindow.Q<Button>("allNamesButton");
            invNameFilter = new(this, allNamesButton, invNamesPanel);

            inventoryPanel.RegisterCallback<MouseEnterEvent>(OnMouseEnter, TrickleDown.TrickleDown);
            inventoryPanel.RegisterCallback<MouseLeaveEvent>(OnMouseLeave, TrickleDown.TrickleDown);
            inventoryPanel.RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
            inventoryPanel.RegisterCallback<MouseDownEvent>(OnMouseDown);
            inventoryPanel.RegisterCallback<MouseMoveEvent>(OnMouseMove);

        }


        public void UpdateItem(Label key, int count, int desiredCount, int row)
        {
            ref Slot slot = ref GetSlot(key);
            bool active = slot.IsRowActive(row);
            slot.ActivateRow(row);
            var cell = slot.Column.ElementAt(0).ElementAt(row);
            cell.Q<UiLabel>(className: "InventoryReqNum").text = NumberToString.Convert(desiredCount);
            cell.Q<UiLabel>(className: "InventoryNum").text = NumberToString.Convert(count);
            if (!active)
                cell.Q("cell").style.visibility = Visibility.Visible;
            TryShowColumn(ref slot);
        }

        private void TryShowColumn(ref Slot slot)
        {
            if (!slot.Visible && invNameFilter.Test(ref slot))
            {
                slot.Visible = true;
                slot.Column.style.display = DisplayStyle.Flex;
            }
        }

        public void ChangeFilters()
        {
            if (!Visible)
            {
                dirty = true;
            }
            else
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    ref var slot = ref slots[i];
                    bool newVisible = invNameFilter.Test(ref slot);
                    if (slot.Visible != newVisible)
                    {
                        slot.Visible = newVisible;
                        slot.Column.style.display = newVisible ? DisplayStyle.Flex : DisplayStyle.None;
                    }
                }
            }
        }

        public void HideItem(Label key, int row)
        {
            ref Slot slot = ref GetSlot(key);
            HideItem(row, ref slot);
        }

        private static void HideItem(int row, ref Slot slot)
        {
            bool active = slot.IsRowActive(row);
            if (active)
            {
                slot.DeactivateRow(row);
                var cell = slot.Column.ElementAt(0).ElementAt(row);
                cell.Q("cell").style.visibility = Visibility.Hidden;
            }
        }

        private ref Slot GetSlot(Label key)
        {
            if (keyToSlot.TryGetValue(key, out int index))
            {
                return ref slots[index];
            }
            else
            {
                keyToSlot.Add(key, slots.Count);
                VisualElement col = columnAsset.Instantiate();
                VisualElement col2 = col.ElementAt(0);
                for (int f = 0; f < col2.childCount; f++)
                {
                    var cell = col2.ElementAt(f);
                    cell.style.display = f < rowCount ? DisplayStyle.Flex : DisplayStyle.None;
                    cell.Q(className: "InventoryImage").style.backgroundImage = new StyleBackground(key.GetSettings().Icon);
                    cell.Q("cell").style.visibility = Visibility.Hidden;
                }
                columnToSlot.Add(col, slots.Count);
                int iconOrder = key.GetSettings().IconOrder;
                slots.Add(new Slot(key, col, iconOrder));
                InsertToPanel(col, iconOrder);
                return ref slots[^1];
            }
        }

        private void InsertToPanel(VisualElement col, int iconOrder)
        {
            if (iconOrder == 0)
            {
                inventoryPanel.Add(col);
            }
            else
            {
                for (int index = 0; ; index++)
                {
                    if (index > inventoryPanel.childCount)
                    {
                        inventoryPanel.Add(col);
                        return;
                    }
                    else if (slots[columnToSlot[inventoryPanel.ElementAt(index)]].IconOrder > iconOrder)
                    {
                        inventoryPanel.Insert(index, col);
                        return;
                    }
                }
            }
        }

        internal void Setup(Inventory inventory)
        {
            primaryInventory = inventory;
            rowCount = inventory.Links.Count;
            dirty = !Visible;

            for (int f = 0; f < inventories.Length; f++)
            {
                if (f >= rowCount || inventories[f] != inventory.Links[f])
                {
                    ClearRow(f);
                    inventories[f] = null;
                }
            }


            if (Visible)
            {
                for (int f = 0; f < rowCount; f++)
                {
                    if (inventories[f] != inventory.Links[f])
                    {
                        inventories[f] = inventory.Links[f];
                        inventories[f].ShowInHud(this, f);
                    }
                }

                for (int f = 0; f < invNamesPanel.childCount; f++)
                {
                    var cell = invNamesPanel.ElementAt(f);
                    cell.style.display = f < rowCount ? DisplayStyle.Flex : DisplayStyle.None;
                    if (f < rowCount)
                    {
                        var nameButton = cell.Q<Button>("nameButton");
                        nameButton.text = inventories[f].Name;
                        nameButton.iconImage = Background.FromSprite(inventories[f].Icon);
                    }
                }

                ChangeFilters();
                ChangeVisibleRows();
            }
        }

        private void ChangeVisibleRows()
        {
            if (prevRowCount != rowCount)
            {
                for (int index = 0; index < inventoryPanel.childCount; index++)
                {
                    var col = inventoryPanel.ElementAt(index);
                    var col2 = col.ElementAt(0);
                    for (int f = 0; f < col2.childCount; f++)
                    {
                        var cell = col2.ElementAt(f);
                        cell.style.display = f < rowCount ? DisplayStyle.Flex : DisplayStyle.None;
                    }
                }

                prevRowCount = rowCount;
            }
        }

        private void ClearRow(int f)
        {
            if (inventories[f] != null)
            {
                invNameFilter.ClearRow(f);
                reqNumManipulator.ConcelIf(inventories[f]);
                itemDragDropManipulator.ConcelIf(inventories[f]);
                inventories[f].StopShowInHud();
                inventories[f] = null;

                foreach (ref var slot in slots.AsSpan())
                {
                    HideItem(f, ref slot);
                }
            }
        }

        private void OnMouseEnter(MouseEnterEvent evt)
        {
            var target = evt.target as VisualElement;
            if (target.name == "InventoryColumn")
                selectedSlot = columnToSlot[target.parent];
        }


        private void OnMouseLeave(MouseLeaveEvent evt)
        {
            var target = evt.target as VisualElement;
            if (target.name == "InventoryColumn")
                selectedSlot = -1;
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0)
                return;

            var target = evt.target as VisualElement;
            if (target.name == "RequestNum" && selectedSlot != -1)
            {
                reqNumManipulator.Start(evt, target, this);
            }
            else
            {
                if (target.parent?.name == "cell" && selectedSlot != -1)
                {
                    itemDragDropManipulator.Start(evt, target.parent, this);
                }

                scrollManipulator.Start(evt, !itemDragDropManipulator.Enabled, this);
            }
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (evt.button != 0)
                return;

            reqNumManipulator.Done(evt, primaryInventory);
            itemDragDropManipulator.Done(evt);
            scrollManipulator.Cancel();
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            reqNumManipulator.Continue(evt);
            itemDragDropManipulator.Continue(evt);
            scrollManipulator.Continue(evt);
        }


        public void CancelManipulators()
        {
            reqNumManipulator.Cancel();
            itemDragDropManipulator.Cancel();
            scrollManipulator.Cancel();
        }

        internal void LinksChanged()
        {
            Setup(primaryInventory);
        }

        internal void SetupIfDirty()
        {
            if (dirty)
                Setup(primaryInventory);
        }

        private class ReqNumManipulator
        {
            private bool enabled;
            private Vector2 mouseStartPos;
            private Inventory inventory;
            private Label key;
            private int startNum;
            private UiLabel target;
            private VisualElement captured;


            public void Start(MouseDownEvent evt, VisualElement target, InventoryVisualizer visualizer)
            {
                int row = target.parent.parent.hierarchy.IndexOf(target.parent);
                inventory = visualizer.inventories[row];
                key = visualizer.SelectedKey;
                ref var slot = ref inventory.FindSlot(key);
                enabled = true;
                mouseStartPos = evt.mousePosition;
                startNum = slot.DesiredCount;
                evt.currentTarget.CaptureMouse();
                this.target = target as UiLabel;
                captured = evt.currentTarget as VisualElement;
            }

            public void Continue(MouseMoveEvent evt)
            {
                if (enabled/* && evt.target.HasMouseCapture() && evt.target == target*/)
                {
                    int newNum = ComputeNewNum(evt);
                    target.text = NumberToString.Convert(newNum);
                }
            }

            internal void Done(MouseUpEvent evt, Inventory mainInventory)
            {
                if (enabled/* && evt.target.HasMouseCapture() && evt.target == target*/)
                {
                    int newNum = ComputeNewNum(evt);
                    target.text = NumberToString.Convert(newNum);
                    ref var slot = ref inventory.FindSlot(key);
                    slot.DesiredCount = newNum;
                    evt.currentTarget.ReleaseMouse();
                    mainInventory?.Balance(key);
                }

                enabled = false;

            }

            private int ComputeNewNum(IMouseEvent evt)
            {
                Vector2 delta = evt.mousePosition - mouseStartPos;
                float d2 = MathF.Abs(delta.x) >= MathF.Abs(delta.y) ? delta.x : -delta.y;
                float num = Mathf.Pow(1.02f, d2) * (startNum + 10) - 10;
                return (int)Mathf.Clamp(num, 0f, 10000f);
            }

            public void Cancel()
            {
                if (enabled)
                {
                    ref var slot = ref inventory.FindSlot(key);
                    target.text = NumberToString.Convert(slot.DesiredCount);
                    captured.ReleaseMouse();
                    enabled = false;
                }
            }

            internal void ConcelIf(Inventory inventory)
            {
                if (enabled && this.inventory == inventory)
                    Cancel();
            }
        }

        private class ItemDragDropManipulator
        {
            private bool enabled;
            private Inventory inventory;
            private Label key;
            private VisualElement captured;
            private InventoryVisualizer visualizer;

            public bool Enabled => enabled;

            public void Start(MouseDownEvent evt, VisualElement target, InventoryVisualizer visualizer)
            {
                int row = target.parent.parent.hierarchy.IndexOf(target.parent);
                this.visualizer = visualizer;
                inventory = visualizer.inventories[row];
                key = visualizer.SelectedKey;
                ref var slot = ref inventory.FindSlot(key);
                if (slot.CountInside > 0)
                {
                    enabled = true;
                    evt.currentTarget.CaptureMouse();
                    captured = evt.currentTarget as VisualElement;
                    visualizer.itemDragElement.style.backgroundImage = new StyleBackground(key.GetSettings().Icon);
                    Continue(evt.mousePosition);
                }
            }

            public void Continue(MouseMoveEvent evt)
            {
                if (enabled)
                {
                    Continue(evt.mousePosition);
                }
            }

            private void Continue(Vector2 mousePosition)
            {
                visualizer.itemDragElement.transform.position = mousePosition;
                int moveCount = GetInventoryUnderMouse(mousePosition)?.CanPaste(inventory, key) ?? 0;
                Color color = moveCount > 0 ? new Color(0.2f, 0.7f, 0.2f) : new Color(0.6f, 0.2f, 0.2f);
                visualizer.itemDragElement.style.borderBottomColor = color;
                visualizer.itemDragElement.style.borderLeftColor = color;
                visualizer.itemDragElement.style.borderRightColor = color;
                visualizer.itemDragElement.style.borderTopColor = color;
                visualizer.itemDragNum.text = NumberToString.Convert(moveCount);
            }

            internal void Done(MouseUpEvent evt)
            {
                if (enabled)
                {
                    var inventory2 = GetInventoryUnderMouse(evt.mousePosition);
                    if (inventory2 != null)
                    {
                        inventory2.TryPaste(inventory, key);
                    }
                    Cleanup();
                }
            }

            Inventory GetInventoryUnderMouse(Vector2 mousePosition)
            {
                var pickedEl = visualizer.inventoryPanel.panel.Pick(mousePosition);
                if (pickedEl?.parent?.name == "cell")
                {
                    var cellEl = pickedEl.parent;
                    int row2 = cellEl.parent.parent.hierarchy.IndexOf(cellEl.parent);
                    return visualizer.inventories[row2];
                }
                else if (pickedEl?.name == "cellFrame")
                {
                    var cellFr = pickedEl;
                    int row2 = cellFr.parent.hierarchy.IndexOf(cellFr);
                    return visualizer.inventories[row2];
                }
                return null;
            }

            public void Cancel()
            {
                if (enabled)
                    Cleanup();
            }

            internal void ConcelIf(Inventory inventory)
            {
                if (enabled && this.inventory == inventory)
                    Cancel();
            }

            private void Cleanup()
            {
                captured.ReleaseMouse();
                enabled = false;
                visualizer.itemDragElement.transform.position = Vector3.left * 500;
            }
        }

        private class ScrollManipulator
        {
            private bool enabled;
            private VisualElement captured;
            private InventoryVisualizer visualizer;


            public void Start(MouseDownEvent evt, bool capture, InventoryVisualizer visualizer)
            {
                this.visualizer = visualizer;
                enabled = true;
                if (capture)
                {
                    evt.currentTarget.CaptureMouse();
                    captured = evt.currentTarget as VisualElement;
                }
            }

            public void Continue(MouseMoveEvent evt)
            {
                if (enabled)
                {
                    var delta = evt.mouseDelta;
                    var scroller = visualizer.scrollView.horizontalScroller;
                    float newValue = scroller.value - delta.x;
                    newValue = Mathf.Clamp(newValue, scroller.lowValue, scroller.highValue);
                    scroller.value = newValue;
                }
            }



            public void Cancel()
            {
                if (enabled)
                {
                    captured?.ReleaseMouse();
                    captured = null;
                    enabled = false;
                }
            }
        }
    }
}
