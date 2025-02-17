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
        private readonly Dictionary<Label, int> keyToSlot = new();
        private readonly Dictionary<VisualElement, int> columnToSlot = new();
        private readonly Stack<VisualElement> columnPool = new();

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
                characters[f].Inventory.ShowInHud(this, f);
            }

            //foreach (ref Slot slot in slots.AsSpan())
            //{
            //    inventory.Add(slot.Column);
            //}
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
    }
}
