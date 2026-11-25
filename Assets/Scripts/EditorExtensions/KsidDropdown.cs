#if UNITY_EDITOR
using Assets.Scripts.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Assets.Scripts.EditorExtensions
{
    // Vyber Ksid s vyhledavanim.
    public class KsidDropdown : AdvancedDropdown
    {
        private readonly List<Ksid> items;
        private readonly Action<Ksid> onSelected;

        public KsidDropdown(IEnumerable<Ksid> items, Action<Ksid> onSelected)
            : base(new AdvancedDropdownState())
        {
            this.items = items.Where(k => k != Ksid.Unknown).OrderBy(k => k.ToString()).ToList();
            this.onSelected = onSelected;
            minimumSize = new Vector2(220, 320);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Ksid");
            foreach (var k in items)
                root.AddChild(new Item(k));
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item) => onSelected(((Item)item).Ksid);

        private class Item : AdvancedDropdownItem
        {
            public readonly Ksid Ksid;
            public Item(Ksid ksid) : base(ksid.ToString()) => Ksid = ksid;
        }
    }
}
#endif
