using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UiLabel = UnityEngine.UIElements.Label;

namespace Assets.Scripts.Core.Inventory
{
    public enum AnnounceStyle
    {
        Info,
        Warning,
    }

    // Texty zprav jako konstanty - Announce pak nealokuje.
    public static class Announcements
    {
        public const string NotEnoughEnergy = "Nemáš dost energie.";
        public const string BuildBlocked = "Stavba blokována.";
        public const string OutOfReach = "Mimo dosah.";
        public const string NotEnoughMaterial = "Nedostatek materiálu.";
    }

    /// <summary>
    /// Dulezita upozorneni v HUDu. Zpravy jdou z fronty postupne: s cekajici frontou se zprava ukaze kratce, jinak dlouho; konci fade outem.
    /// Zobrazuje se uprostred oblasti mezi Top a Bottom, nikdy ne nad polovinou obrazovky.
    /// </summary>
    public class Announcer
    {
        private const float ShortTime = 1f;
        private const float LongTime = 2f;
        private const float FadeTime = 0.3f;

        private static readonly string[] styleClasses = { "AnnounceInfo", "AnnounceWarning" };

        private struct Message
        {
            public string Text;
            public AnnounceStyle Style;

            public readonly bool Same(string text, AnnounceStyle style) => Style == style && Text == text;
        }

        private readonly VisualElement root;
        private readonly VisualElement top;
        private readonly VisualElement area;
        private readonly UiLabel label;
        private readonly Queue<Message> queue = new();   // neomezena, alokuje jen pri rustu kapacity
        private Message current;
        private bool showing;
        private float time;

        public Announcer(VisualElement root)
        {
            this.root = root;
            top = root.Q<VisualElement>("Top");
            area = root.Q<VisualElement>("AnnouncerArea");
            label = area.Q<UiLabel>("AnnouncerLabel");
            label.style.display = DisplayStyle.None;
            top.RegisterCallback<GeometryChangedEvent>(OnLayoutChanged);
            root.RegisterCallback<GeometryChangedEvent>(OnLayoutChanged);
        }

        public void Announce(string text, AnnounceStyle style)
        {
            if (showing && current.Same(text, style))
            {
                time = 0;
                return;
            }
            foreach (var m in queue)
            {
                if (m.Same(text, style))
                    return;
            }

            var msg = new Message { Text = text, Style = style };
            if (!showing)
            {
                Show(msg);
                return;
            }

            if (queue.Count == 0)
                ShortenCurrent();
            queue.Enqueue(msg);
        }

        public void Update(float deltaTime)
        {
            if (!showing)
                return;

            time += deltaTime;
            float duration = queue.Count > 0 ? ShortTime : LongTime;
            if (time >= duration)
            {
                if (queue.Count > 0)
                    Show(queue.Dequeue());
                else
                    Hide();
                return;
            }

            float fadeLeft = duration - time;
            label.style.opacity = fadeLeft < FadeTime ? fadeLeft / FadeTime : 1f;
        }

        // Prechod z dlouheho na kratky cas. Za fazi kratke zpravy rovnou zacne mizet, rozbehly fade pokracuje plynule.
        private void ShortenCurrent()
        {
            if (time <= ShortTime - FadeTime)
                return;
            float fadeLeft = time > LongTime - FadeTime ? LongTime - time : FadeTime;
            time = ShortTime - fadeLeft;
        }

        private void Show(Message msg)
        {
            if (showing)
                label.RemoveFromClassList(styleClasses[(int)current.Style]);
            current = msg;
            showing = true;
            time = 0;
            label.text = msg.Text;
            label.AddToClassList(styleClasses[(int)msg.Style]);
            label.style.opacity = 1f;
            label.style.display = DisplayStyle.Flex;
        }

        private void Hide()
        {
            label.RemoveFromClassList(styleClasses[(int)current.Style]);
            current = default;
            showing = false;
            label.style.display = DisplayStyle.None;
        }

        // Oblast zprav zacina nejvyse v pulce obrazovky, jinak hned pod Top (otevreny inventar).
        private void OnLayoutChanged(GeometryChangedEvent evt)
        {
            float padding = Mathf.Max(0f, root.layout.height * 0.5f - top.layout.height);
            area.style.paddingTop = padding;
        }
    }
}
