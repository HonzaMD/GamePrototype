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
        private float fpsTimeStamp;
        private int framesCount;
        private InventoryVisualizer inventoryVisualizer;
        private Label[] quickSlotKeys = new Label[10];

        public Label SelectedInventoryKey => inventoryVisualizer.SelectedKey;

        private void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            fpsLabel = doc.rootVisualElement.Q<UiLabel>("FpsLabel");
            console = doc.rootVisualElement.Q<VisualElement>("Console");
            inventory = doc.rootVisualElement.Q<VisualElement>("Inventory");
            quickSlots = doc.rootVisualElement.Q<VisualElement>("QuickSlots");
            InitQuickSlots(quickSlots);
            inventoryVisualizer = new InventoryVisualizer(inventory, ColumnTree);

            Application.logMessageReceived += Application_logMessageReceived;
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
        }

        internal void SetupInventory(List<Character3> characters)
        {
            inventoryVisualizer.Setup(characters);
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
    }
}
