using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Core
{
    public class KsidNode
    {
        public Ksid Name { get; }
        public KsidNode[] Parents { get; private set; }
        public KsidNode[] Children { get; private set; }

        private int tag;
        private readonly ushort component;
        private readonly Ksids builder;
        private ushort index = ushort.MaxValue;
        private byte bit;
        private byte[] parentBits;

        public KsidNode(Ksid name, ushort component, Ksids builder)
        {
            Name = name;
            this.component = component;
            this.builder = builder;
        }

        internal void InitDependencies(List<Ksid> parents, List<Ksid> children)
        {
            Parents = parents.Count == 0 ? Array.Empty<KsidNode>() : parents.Select(p => builder[p]).ToArray();
            Children = children.Count == 0 ? Array.Empty<KsidNode>() : children.Select(c => builder[c]).ToArray();
        }

        internal void ResetTag() => tag = 0;

        public bool IsMyParent(KsidNode parent)
        {
            if (component != parent.component)
                return false;
            if (parentBits == null)
                BuildParentBits();
            return parent.index < parentBits.Length && (parentBits[parent.index] & parent.bit) != 0;
        }

        private void BuildParentBits()
        {
            int biggestIndex = -1;

            SearchParents(ksid =>
            {
                if (ksid.index == ushort.MaxValue)
                    ksid.InitIndex();
                if (biggestIndex < ksid.index)
                    biggestIndex = ksid.index;
            });

            if (biggestIndex == -1)
            {
                parentBits = Array.Empty<byte>();
            }
            else
            {
                parentBits = new byte[biggestIndex + 1];

                SearchParents(ksid => parentBits[ksid.index] |= ksid.bit);
            }
        }

        private void InitIndex()
        {
            var i = builder.GetNextComponentIndex(component);
            var bitPos = i & 7;
            index = (ushort)(i >> 3);
            bit = (byte)(1 << bitPos);
        }

        public void SearchParents(Action<KsidNode> action)
        {
            tag = builder.GetNextTag();
            foreach (var p in Parents)
                p.SearchParents(tag, action);
        }

        private void SearchParents(int tag, Action<KsidNode> action)
        {
            if (this.tag != tag)
            {
                this.tag = tag;
                action(this);
                foreach (var p in Parents)
                    p.SearchParents(tag, action);
            }
        }
    }
}
