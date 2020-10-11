using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Core
{
    public class Ksids
    {
        private readonly Dictionary<int, Ksid> ksids;
        private readonly int[] componentIndexes;
        private int currentTag;

        public Ksids(IEnumerable<(KsidEnum child, KsidEnum parent)> dependencies)
        {
            var tempDict = ((int[])Enum.GetValues(typeof(KsidEnum))).ToDictionary(n => n, n => new TempKsid());
            InitDependencies(tempDict, dependencies);
            DetectCycles(tempDict);
            int componentCount = SetComponents(tempDict);
            ksids = tempDict.ToDictionary(p => p.Key, p => new Ksid((KsidEnum)p.Key, (ushort)p.Value.component, this));
            AssignDependencies(tempDict);
            componentIndexes = new int[componentCount];
        }

        public Dictionary<int, Ksid>.ValueCollection AllKsids => ksids.Values;

        public Ksid this[KsidEnum name]
        {
            get => ksids[(int)name];
        }


        public bool IsParent(KsidEnum child, KsidEnum parent) => this[child].IsMyParent(this[parent]);
        public bool IsParentOrEqual(KsidEnum child, KsidEnum parent) => child == parent || this[child].IsMyParent(this[parent]);



        private static void InitDependencies(Dictionary<int, TempKsid> tempDict, IEnumerable<(KsidEnum child, KsidEnum parent)> dependencies)
        {
            foreach (var dep in dependencies)
            {
                tempDict[(int)dep.child].Parents.Add(dep.parent);
                tempDict[(int)dep.parent].Children.Add(dep.child);
            }
        }

        private void AssignDependencies(Dictionary<int, TempKsid> tempDict)
        {
            foreach (var p in tempDict)
                ksids[p.Key].InitDependencies(p.Value.Parents, p.Value.Children);
        }

        private void DetectCycles(Dictionary<int, TempKsid> tempDict)
        {
            foreach (var tk in tempDict.Values)
            {
                DetectCycles(tk, tempDict);
            }
        }

        private void DetectCycles(TempKsid tk, Dictionary<int, TempKsid> tempDict)
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

        private static int SetComponents(Dictionary<int, TempKsid> tempDict)
        {
            int component = 0;
            foreach (var tk in tempDict.Values)
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

        private static void SetComponents(int component, TempKsid tk, Dictionary<int, TempKsid> tempDict)
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
            public List<KsidEnum> Parents = new List<KsidEnum>();
            public List<KsidEnum> Children = new List<KsidEnum>();
            public int component = -1;
        }
    }
}
