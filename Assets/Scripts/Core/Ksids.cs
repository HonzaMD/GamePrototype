using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Core
{
    public class Ksids
    {
        private readonly KsidNode[] ksids;
        private readonly int[] componentIndexes;
        private int currentTag;

        public Ksids(IEnumerable<(Ksid child, Ksid parent)> dependencies)
        {
            var tempDict = ((int[])Enum.GetValues(typeof(Ksid))).Select(n => new TempKsid()).ToArray();
            InitDependencies(tempDict, dependencies);
            DetectCycles(tempDict);
            int componentCount = SetComponents(tempDict);
            ksids = tempDict.Select((tk, i) => new KsidNode((Ksid)i, (ushort)tk.component, this)).ToArray();
            AssignDependencies(tempDict);
            componentIndexes = new int[componentCount];
        }

        public KsidNode[] AllKsids => ksids;

        public KsidNode this[Ksid name]
        {
            get => ksids[(int)name];
        }


        public bool IsParent(Ksid child, Ksid parent) => this[child].IsMyParent(this[parent]);
        public bool IsParentOrEqual(Ksid child, Ksid parent) => child == parent || this[child].IsMyParent(this[parent]);



        private static void InitDependencies(TempKsid[] tempDict, IEnumerable<(Ksid child, Ksid parent)> dependencies)
        {
            foreach (var dep in dependencies)
            {
                tempDict[(int)dep.child].Parents.Add(dep.parent);
                tempDict[(int)dep.parent].Children.Add(dep.child);
            }
        }

        private void AssignDependencies(TempKsid[] tempDict)
        {
            for (int f = 0; f < tempDict.Length; f++)
            {
                ksids[f].InitDependencies(tempDict[f].Parents, tempDict[f].Children);
            }
        }

        private void DetectCycles(TempKsid[] tempDict)
        {
            foreach (var tk in tempDict)
            {
                DetectCycles(tk, tempDict);
            }
        }

        private void DetectCycles(TempKsid tk, TempKsid[] tempDict)
        {
            if (tk.component == -2)
                throw new InvalidOperationException("V grafu KSID jmen je cyklus!");
            if (tk.component == -1)
            {
                tk.component = -2;
                foreach (var ch in tk.Children)
                {
                    DetectCycles(tempDict[(int)ch], tempDict);
                }
                tk.component = -3;
            }
        }

        private static int SetComponents(TempKsid[] tempDict)
        {
            int component = 0;
            foreach (var tk in tempDict)
            {
                if (tk.component == -3)
                {
                    if (component > ushort.MaxValue)
                        throw new InvalidOperationException("Dosly mi ksid komponenty");
                    SetComponents(component, tk, tempDict);
                    component++;
                }
            }
            return component;
        }

        private static void SetComponents(int component, TempKsid tk, TempKsid[] tempDict)
        {
            if (tk.component == -3)
            {
                tk.component = component;
                foreach (var ch in tk.Children)
                {
                    SetComponents(component, tempDict[(int)ch], tempDict);
                }
                foreach (var p in tk.Parents)
                {
                    SetComponents(component, tempDict[(int)p], tempDict);
                }
            }
            else if (tk.component != component)
            {
                throw new InvalidOperationException("Divnost, cekal jsem ze tenhle vrchol bude take v me komponente");
            }
        }

        internal int GetNextTag()
        {
            currentTag++;
            if (currentTag == 0)
                ResetTags();
            return currentTag;
        }

        private void ResetTags()
        {
            currentTag++;
            foreach (var ksid in AllKsids)
                ksid.ResetTag();
        }

        internal int GetNextComponentIndex(ushort component)
        {
            var ret = componentIndexes[component];
            if ((ret >> 3) >= ushort.MaxValue)
                throw new InvalidOperationException("Dosly mi component indexy!");
            componentIndexes[component] = ret + 1;
            return ret;
        }

        private class TempKsid
        {
            public List<Ksid> Parents = new List<Ksid>();
            public List<Ksid> Children = new List<Ksid>();
            public int component = -1;
        }
    }
}
