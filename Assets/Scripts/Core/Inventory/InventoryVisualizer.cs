using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using UnityEditor.Graphs;
using UnityEngine;
using UnityEngine.UIElements;
using UiLabel = UnityEngine.UIElements.Label;

namespace Assets.Scripts.Core.Inventory
{
    internal class InventoryVisualizer
    {
        private readonly SpanList<Slot> slots = new();
        private readonly List<Inventory> inventories = new();
        private readonly Dictionary<Label, int> keyToSlot = new();
        private readonly Dictionary<VisualElement, int> columnToSlot = new();
        private readonly Stack<VisualElement> columnPool = new();
        private readonly ReqNumManipulator reqNumManipulator = new();

        private readonly VisualElement inventory;
        private readonly VisualTreeAsset columnAsset;
        private int rowCount;
        private int selectedColumn = -1;

        public Label SelectedKey => selectedColumn != -1 ? slots[selectedColumn].Key : null;

        private struct Slot
        {
            public readonly Label Key;
            public int Pos;
            public readonly VisualElement Column;
            private int activeRows;
            
            public void ActivateRow(int row) => activeRows |= (1 << row);
            public void DeactivateRow(int row) => activeRows &= ~(1 << row);
            public bool isRowActive(int row) => ((1 << row) & activeRows) != 0;

            public Slot(Label key, VisualElement column)
            {
                Key = key;
                Column = column;
                Pos = -1;
                activeRows = 0;
            }
        }

        public InventoryVisualizer(VisualElement inventory, VisualTreeAsset columnAsset)
        {
            this.inventory = inventory;
            this.columnAsset = columnAsset;

            inventory.RegisterCallback<MouseEnterEvent>(OnMouseEnter, TrickleDown.TrickleDown);
            inventory.RegisterCallback<MouseLeaveEvent>(OnMouseLeave, TrickleDown.TrickleDown);
            inventory.RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
            inventory.RegisterCallback<MouseDownEvent>(OnMouseDown);
            inventory.RegisterCallback<MouseMoveEvent>(OnMouseMove);

        }


        public void UpdateItem(Label key, int count, int desiredCount, int row)
        {
            ref Slot slot = ref GetSlot(key);
            bool active = slot.isRowActive(row);
            slot.ActivateRow(row);
            var cell = slot.Column.ElementAt(0).ElementAt(row);         
            cell.Q<UiLabel>(className: "InventoryReqNum").text = NumberToString.Convert(desiredCount);
            cell.Q<UiLabel>(className: "InventoryNum").text = NumberToString.Convert(count);
            if (!active)
                cell.Q("cell").style.visibility = Visibility.Visible;
        }

        public void HideItem(Label key, int row)
        {
            ref Slot slot = ref GetSlot(key);
            bool active = slot.isRowActive(row);
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
                VisualElement col = columnPool.Count > 0 ? columnPool.Pop() : columnAsset.Instantiate();
                VisualElement col2 = col.ElementAt(0);
                for (int f = 0; f < col2.childCount; f++)
                {
                    var cell = col2.ElementAt(f);
                    cell.style.display = f < rowCount ? DisplayStyle.Flex : DisplayStyle.None;
                    cell.Q(className: "InventoryImage").style.backgroundImage = new StyleBackground(key.GetSettings().Icon);
                    cell.Q("cell").style.visibility = Visibility.Hidden;
                }
                columnToSlot.Add(col, slots.Count);
                slots.Add(new Slot(key, col));
                inventory.Add(col);
                return ref slots[^1];
            }
        }

        internal void Setup(List<Character3> characters)
        {
            rowCount = Math.Min(characters.Count, 4);
            for (int f = 0; f < rowCount; f++)
            {
                var inventory = characters[f].Inventory;
                inventories.Add(inventory);
                inventory.ShowInHud(this, f);

                for (int g = 0; g < rowCount; g++)
                {
                    if (g != f)
                        inventory.AddLink(characters[g].Inventory);
                }
            }
        }

        private void OnMouseEnter(MouseEnterEvent evt)
        {
            var target = evt.target as VisualElement;
            if (target.name == "InventoryColumn")
                selectedColumn = columnToSlot[target.parent];
        }


        private void OnMouseLeave(MouseLeaveEvent evt)
        {
            var target = evt.target as VisualElement;
            if (target.name == "InventoryColumn")
                selectedColumn = -1;
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            var target = evt.target as VisualElement;
            if (target.name == "RequestNum" && selectedColumn != -1)
            {
                reqNumManipulator.Start(evt, target, this);
            }
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            reqNumManipulator.Done(evt, inventories.Count > 0 ? inventories[0] : null);
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            reqNumManipulator.Continue(evt);
        }


        public void CancelManipulators()
        {
            reqNumManipulator.Cancel();
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
                Debug.Log($"ReqNum {visualizer.selectedColumn} {row} desired: {slot.DesiredCount}");
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
        }
    }
}
