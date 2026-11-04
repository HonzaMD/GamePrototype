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
        private bool statusRowDirty;
        private StatusEntryView[] statusViews = Array.Empty<StatusEntryView>();

        public Announcer Announcer { get; private set; }
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
            Announcer = new Announcer(doc.rootVisualElement);

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
            Announcer.Update(Time.unscaledDeltaTime);

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

            bool detail = inventoryVisualizer.Visible;
            for (int i = 0; i < chars.Count; i++)
                statusViews[i].Update(chars[i].Status, chars[i] == ic.Character, detail);
        }

        private void RebuildStatusRow(List<Character3> chars, InputController ic)
        {
            statusRow.Clear();
            statusViews = new StatusEntryView[chars.Count];

            for (int i = 0; i < chars.Count; i++)
            {
                var view = new StatusEntryView(chars[i].Status);
                int index = i;
                view.Entry.RegisterCallback<ClickEvent>(evt => ic.SetCharacter(index));
                // UI Toolkit kresli v poradi deti. StatusRow je row-reverse => prvni postava je posledni dite a jeji tooltip
                // se kresli nad dalsimi postavami. Ze stejneho duvodu je StatusRow v Top (column-reverse) az za inventarem.
                statusRow.Insert(0, view.Entry);
                statusViews[i] = view;
            }
        }

        /// <summary>
        /// Polozka postavy ve StatusRow. Strucny rezim: zdravi, energie, varovani. Detail (otevreny inventar): navic Food, SleepEnergy, Waste.
        /// </summary>
        private sealed class StatusEntryView
        {
            public readonly VisualElement Entry;
            private readonly StatusBar health;
            private readonly StatusBar energy;
            private readonly VisualElement detail;
            private readonly StatusBar food;
            private readonly StatusBar sleepEnergy;
            private readonly StatusBar waste;
            private readonly VisualElement warnings;
            private readonly VisualElement hungry;
            private readonly VisualElement needSleep;
            private readonly VisualElement needShit;

            public StatusEntryView(Status status)
            {
                Entry = new VisualElement();
                Entry.AddToClassList("StatusEntry");

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

                Entry.Add(left);

                health = new StatusBar(Entry, "StatusFillHealth", "Zdraví:");
                energy = new StatusBar(Entry, "StatusFillEnergy", "Energie:");

                detail = new VisualElement();
                detail.AddToClassList("StatusDetail");
                Entry.Add(detail);
                food = new StatusBar(detail, "StatusFillFood", "Jídlo:");
                sleepEnergy = new StatusBar(detail, "StatusFillSleep", "Spánek:");
                waste = new StatusBar(detail, "StatusFillWaste", "Hnědá:");

                warnings = new VisualElement();
                warnings.AddToClassList("StatusWarnings");
                Entry.Add(warnings);
                hungry = AddWarning("StatusFillFood", "Potřebuješ jíst.");
                needSleep = AddWarning("StatusFillSleep", "Potřebuješ spát.");
                needShit = AddWarning("StatusFillWaste", "Potřebuješ na záchod.");
            }

            private VisualElement AddWarning(string colorClass, string text)
            {
                var w = new VisualElement();
                w.AddToClassList("StatusWarning");
                w.AddToClassList(colorClass);
                AddTooltip(w, text);
                warnings.Add(w);
                return w;
            }

            public void Update(Status status, bool selected, bool showDetail)
            {
                Entry.EnableInClassList(StatusEntrySelectedClass, selected);

                float max = status.MaxHealth;
                health.SetFraction(max > 0 ? status.CurrentHealth / max : 0, status.CurrentHealth);
                energy.SetSegmented(status.Energy);

                SetVisible(detail, showDetail);
                if (showDetail)
                {
                    food.SetSegmented(status.Food);
                    sleepEnergy.SetSegmented(status.SleepEnergy);
                    waste.SetSegmented(status.Waste);
                }

                bool isHungry = status.Hungry;
                bool isSleepy = status.NeedSleep;
                bool isFull = status.NeedShit || status.IsOverEated;
                SetVisible(hungry, isHungry);
                SetVisible(needSleep, isSleepy);
                SetVisible(needShit, isFull);
                SetVisible(warnings, isHungry || isSleepy || isFull);
            }

            private static void SetVisible(VisualElement e, bool visible) => e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// Tooltip zobrazeny pri hoveru nad parentem (viz USS). Staticky text se nastavi jednou; promennou cast lze pridat dalsim labelem do vraceneho kontejneru.
        /// </summary>
        // TODO, pokud predelame tooltipy na neco robustnejsiho. Predelat i zde.
        private static VisualElement AddTooltip(VisualElement parent, string text)
        {
            var tooltip = new VisualElement();
            tooltip.AddToClassList("StatusTooltip");
            tooltip.pickingMode = PickingMode.Ignore;
            tooltip.Add(CreateTooltipText(text));
            parent.Add(tooltip);
            return tooltip;
        }

        private static UiLabel CreateTooltipText(string text)
        {
            var label = new UiLabel(text);
            label.AddToClassList("StatusTooltipText");
            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        /// <summary>
        /// Svisly bar. Bud jeden segment 0..max (SetFraction), nebo segmenty po 100 vedle sebe, ktere se objevuji a mizi podle hodnoty a plni se zleva (SetSegmented).
        /// </summary>
        private sealed class StatusBar
        {
            private const float SegmentSize = 100;
            private readonly VisualElement root;
            private readonly string fillClass;
            private readonly UiLabel label;
            private readonly List<VisualElement> segments = new();
            private readonly List<VisualElement> fills = new();
            private int shownSegments;
            private int shownValue = int.MinValue;

            public StatusBar(VisualElement parent, string fillClass, string caption)
            {
                this.fillClass = fillClass;
                root = new VisualElement();
                root.AddToClassList("StatusBar");
                // caption a cislo jako 2 labely, cislo z cache NumberToString => bez alokaci
                label = CreateTooltipText("");
                label.AddToClassList("StatusTooltipValue");
                AddTooltip(root, caption).Add(label);
                parent.Add(root);
            }

            public void SetFraction(float fraction, float value)
            {
                SetSegmentCount(1);
                SetFill(0, fraction);
                SetLabel(value);
            }

            public void SetSegmented(float value)
            {
                int count = Mathf.Max(1, Mathf.CeilToInt(value / SegmentSize));
                SetSegmentCount(count);
                for (int i = 0; i < count; i++)
                    SetFill(i, (value - i * SegmentSize) / SegmentSize);
                SetLabel(value);
            }

            private void SetSegmentCount(int count)
            {
                if (count == shownSegments)
                    return;
                while (segments.Count < count)
                {
                    var segment = new VisualElement();
                    segment.AddToClassList("StatusBarSegment");
                    var fill = new VisualElement();
                    fill.AddToClassList("StatusBarFill");
                    fill.AddToClassList(fillClass);
                    segment.Add(fill);
                    root.Insert(segments.Count, segment); // tooltip zustava posledni, kresli se navrch
                    segments.Add(segment);
                    fills.Add(fill);
                }
                for (int i = 0; i < segments.Count; i++)
                    segments[i].style.display = i < count ? DisplayStyle.Flex : DisplayStyle.None;
                shownSegments = count;
            }

            private void SetFill(int index, float fraction) => fills[index].style.height = Length.Percent(Mathf.Clamp01(fraction) * 100f);

            private void SetLabel(float value)
            {
                int v = (int)value;
                if (v != shownValue)
                {
                    shownValue = v;
                    label.text = NumberToString.Convert(v);
                }
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
