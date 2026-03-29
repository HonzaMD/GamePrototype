using Assets.Scripts.Bases;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UiLabel = UnityEngine.UIElements.Label;

namespace Assets.Scripts.Core.Inventory
{
    public class Hud : MonoBehaviour
    {
        public VisualTreeAsset ColumnTree;

        private UiLabel fpsLabel;
        private VisualElement console;
        private VisualElement inventory;
        private VisualElement quickSlots;
        private VisualElement inventoryWindow;
        private float fpsTimeStamp;
        private int framesCount;
        private InventoryVisualizer inventoryVisualizer;
        private Label[] quickSlotKeys = new Label[10];
        private bool guiInFocus;
        private const string StatusEntrySelectedClass = "StatusEntrySelected";
        private VisualElement statusRow;
        private int statusRowCount;
        private bool statusRowDirty;
        private VisualElement[] statusHealthFills = Array.Empty<VisualElement>();
        private VisualElement[] statusEntries = Array.Empty<VisualElement>();
        private UiLabel[] statusHealthLabels = Array.Empty<UiLabel>();
        private int[] statusHealthCache = Array.Empty<int>();

        public Label SelectedInventoryKey => inventoryVisualizer.SelectedKey;
        public bool GuiInFocus => guiInFocus;

        private void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            fpsLabel = doc.rootVisualElement.Q<UiLabel>("FpsLabel");
            console = doc.rootVisualElement.Q<VisualElement>("Console");
            inventory = doc.rootVisualElement.Q<VisualElement>("Inventory");
            quickSlots = doc.rootVisualElement.Q<VisualElement>("QuickSlots");
            inventoryWindow = doc.rootVisualElement.Q<VisualElement>("InventoryWindow");
            var scrollView = inventoryWindow.Q<ScrollView>();
            var itemDragElement = doc.rootVisualElement.Q<VisualElement>("itemDragElement");
            statusRow = doc.rootVisualElement.Q<VisualElement>("StatusRow");
            DisableFocus(doc.rootVisualElement);

            InitQuickSlots(quickSlots);
            inventoryWindow.style.display = DisplayStyle.None;
            inventoryVisualizer = new InventoryVisualizer(inventory, inventoryWindow, ColumnTree, itemDragElement, scrollView);

            Application.logMessageReceived += Application_logMessageReceived;
            inventoryWindow.RegisterCallback<MouseEnterEvent>(GuiOnMouseEnter);
            inventoryWindow.RegisterCallback<MouseLeaveEvent>(GuiOnMouseLeave);
        }

        private void DisableFocus(VisualElement rootVisualElement)
        {
            foreach(var button in rootVisualElement.Query<Button>().Build())
            {
                button.focusable = false;
            }
        }

        private void InitQuickSlots(VisualElement quickSlots)
        {
            for (int i = 0; i < 10; i++)
            {
                string text = i == 9 ? "0" : NumberToString.Convert(i + 1);
                quickSlots.ElementAt(i).Q<UiLabel>(className: "QuickSlotKey").text = text;
                quickSlots.ElementAt(i).style.display = DisplayStyle.None;
            }
        }

        private void Application_logMessageReceived(string condition, string stackTrace, LogType type)
        {
            var label = console.ElementAt(0) as UiLabel;
            label.text = condition;
            label.BringToFront();
        }

        private void Update()
        {
            framesCount++;
            fpsTimeStamp += Time.unscaledDeltaTime;
            if (fpsTimeStamp > 1)
            {
                fpsLabel.text = NumberToString.Convert((int)(framesCount / fpsTimeStamp));
                framesCount = 0;
                fpsTimeStamp = 0;
            }

            UpdateStatusRow();

            if (Input.GetKeyDown(KeyCode.I))
            {
                inventoryVisualizer.CancelManipulators();
                inventoryVisualizer.Visible = !inventoryVisualizer.Visible;
                inventoryWindow.style.display = inventoryVisualizer.Visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (inventoryVisualizer.Visible)
                    inventoryVisualizer.SetupIfDirty();
            }                   
        }

        internal void SetupInventory(Inventory inventory)
        {
            inventoryVisualizer.Setup(inventory);
        }

        public void UpdateQuickSlot(int index, Label key, int count) 
        {
            index += 9;
            if (index < 0)
                index = 9;
            var cell = quickSlots.ElementAt(index);
            if (key != null)
            {                
                cell.Q<UiLabel>(className: "InventoryNum").text = NumberToString.Convert(count);
                if (quickSlotKeys[index] != key)
                {
                    cell.style.display = DisplayStyle.Flex;
                    cell.Q(className: "QuickSlotImage").style.backgroundImage = new StyleBackground(key.GetSettings().Icon);
                }
            }
            else if (quickSlotKeys[index] != null)
            {
                cell.style.display = DisplayStyle.None;
            }
            quickSlotKeys[index] = key;
        }

        public void InvalidateStatusRow() => statusRowDirty = true;

        private void UpdateStatusRow()
        {
            var ic = Game.Instance.InputController;
            var chars = ic.Characters;

            if (statusRowDirty)
            {
                statusRowDirty = false;
                RebuildStatusRow(chars, ic);
            }

            for (int i = 0; i < chars.Count; i++)
            {
                var status = chars[i].Status;

                bool selected = chars[i] == ic.Character;
                statusEntries[i].EnableInClassList(StatusEntrySelectedClass, selected);

                int healthInt = (int)status.CurrentHealth;
                float pct = status.MaxHealth > 0 ? status.CurrentHealth / status.MaxHealth : 0;
                statusHealthFills[i].style.height = Length.Percent(Mathf.Clamp01(pct) * 100f);

                if (statusHealthCache[i] != healthInt)
                {
                    // TODO, pokud predelame tooltipy na neco robustnejsiho. Predelat i zde.
                    statusHealthCache[i] = healthInt;
                    statusHealthLabels[i].text = NumberToString.Convert(healthInt);
                }
            }
        }

        private void RebuildStatusRow(List<Character3> chars, InputController ic)
        {
            statusRow.Clear();
            statusRowCount = chars.Count;
            statusEntries = new VisualElement[chars.Count];
            statusHealthFills = new VisualElement[chars.Count];
            statusHealthLabels = new UiLabel[chars.Count];
            statusHealthCache = new int[chars.Count];
            Array.Fill(statusHealthCache, -1);

            for (int i = 0; i < chars.Count; i++)
            {
                var status = chars[i].Status;

                var entry = new VisualElement();
                entry.AddToClassList("StatusEntry");

                var left = new VisualElement();
                left.AddToClassList("StatusLeft");

                var icon = new VisualElement();
                icon.AddToClassList("StatusIcon");
                if (status.Icon != null)
                    icon.style.backgroundImage = new StyleBackground(status.Icon);
                left.Add(icon);

                var nameLabel = new UiLabel();
                nameLabel.AddToClassList("StatusName");
                nameLabel.text = status.Name ?? "";
                left.Add(nameLabel);

                entry.Add(left);

                var healthBar = new VisualElement();
                healthBar.AddToClassList("StatusHealthBar");

                var healthFill = new VisualElement();
                healthFill.AddToClassList("StatusHealthFill");
                healthFill.style.height = Length.Percent(100f);
                healthBar.Add(healthFill);

                var healthLabel = new UiLabel();
                healthLabel.AddToClassList("StatusHealthLabel");
                healthLabel.pickingMode = PickingMode.Ignore;
                healthBar.Add(healthLabel);

                entry.Add(healthBar);

                int index = i;
                entry.RegisterCallback<ClickEvent>(evt => ic.SetCharacter(index));

                statusRow.Add(entry);
                statusEntries[i] = entry;
                statusHealthFills[i] = healthFill;
                statusHealthLabels[i] = healthLabel;
            }
        }

        private void GuiOnMouseEnter(MouseEnterEvent evt)
        {
            guiInFocus = true;
        }


        private void GuiOnMouseLeave(MouseLeaveEvent evt)
        {
            guiInFocus = false;
        }
    }
}
