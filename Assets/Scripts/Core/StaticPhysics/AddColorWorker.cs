using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Core.StaticPhysics
{
    // Rozsiri barevny hrany
    // Nejprve overim toUpdate mnozinu, zda nema nejake uzly ze kterych lze rozsirovat
    // Rozsirovani:
    //  ShortestColorDistance - nejkratsi vzdalenost z uzlu do korene = min (barevny delky hran)
    //  mohu rozsirovat jen do uzlu ktery ma SCD > moji SCD (jinak bych vyrabel zpetne hrany)
    //  rozsirenim spocitam barevnou delku hrany = SCD + delka hrany
    //  tim mohu zmencit SCD uzlu kam se rozsiruji, pokud se tak stane, stava se uzel dalsim kandidatem na rozsirovani
    // Praci si radim podle SCD (od nejmensiho) tim mam zaruceno, ze hodnota SCD se kterou pracuju uz je spravne (nemuze existovat nespracovany uzel ktery by mi ji vylepsil)
    // Pro kazdou barvu, zpracovavam kazdy uzel max jednou

    internal class AddColorWorker
    {
        private readonly SpDataManager data;
        private readonly HashSet<int> toUpdate;
        private readonly HashSet<int> deletedNodes;
        private readonly ConsistencyWorker consistencyWorker;
        private readonly BinaryHeap<Work> workQueue = new BinaryHeap<Work>();

        private struct Work : IComparable<Work>
        {
            public int Node;
            public int Color;
            public float Length;

            public int CompareTo(Work other) => (int)Mathf.Sign(Length - other.Length);
        }

        public AddColorWorker(SpDataManager data, HashSet<int> toUpdate, HashSet<int> deletedNodes, ConsistencyWorker consistencyWorker)
        {
            this.data = data;
            this.toUpdate = toUpdate;
            this.deletedNodes = deletedNodes;
            this.consistencyWorker = consistencyWorker;
        }

        internal void Run()
        {
            DetectWork();
            while (workQueue.Count > 0)
            {
                ExpandColor();
            }
        }

        private void DetectWork()
        {
            foreach (int index in toUpdate)
            {
                if (!deletedNodes.Contains(index))
                {
                    ref var node = ref data.GetNode(index);

                    TestColorCandidate(index, node, 0, node.isFixedRoot);
                    for (int f = 0; f < node.newEdges.Length; f++)
                    {
                        TestColorCandidate(index, node, f, node.newEdges[f].Out0Root);
                        TestColorCandidate(index, node, f, node.newEdges[f].Out1Root);
                    }
                }
            }
        }

        private void TestColorCandidate(int index, in SpNode node, int f, int color)
        {
            if (IsFirstColor(color, node.newEdges, f) && CanExtend(node, color, out float startDist))
                workQueue.Add(new Work() { Color = color, Length = startDist, Node = index });
        }

        private bool IsFirstColor(int color, EdgeEnd[] newEdges, int f)
        {
            if (color == 0)
                return false;
            for (int i = 0; i < f; i++)
            {
                if (newEdges[i].Out0Root == color)
                    return false;
                if (newEdges[i].Out1Root == color)
                    return false;
            }
            return true;
        }

        private bool CanExtend(in SpNode node, int color, out float startDist)
        {
            startDist = node.ShortestColorDistanceNew(color);

            var edges = node.newEdges;
            for (int f = 0; f < edges.Length; f++)
            {
                if (edges[f].In0Root != color && edges[f].In1Root != color) // pokud prichozi hrana existuje tak by mela byt spravne
                {
                    int indexB = edges[f].Other;
                    ref var nodeB = ref data.GetNode(indexB);
                    float otherDist = nodeB.ShortestColorDistanceAny(color);
                    if (otherDist > startDist)
                    {
                        if (edges[f].In1Root == 0 || edges[f].In0Root == 0 || Utils.IsDistanceBetter(startDist, node.ShortestColorDistanceNew(edges[f].In1Root), color, edges[f].In1Root))
                            return true;
                    }
                }
            }
            return false;
        }

        private void ExpandColor()
        {
            var work = workQueue.Remove();
            ref var node = ref data.GetNode(work.Node);
            int color = work.Color;
            float startDist = node.ShortestColorDistanceNew(color);
            if (startDist != work.Length)
                return; // dostal jsem se sem rychleji
            float outStrength = node.FindOutStrengthNew(color);

            var edges = node.newEdges;
            for (int f = 0; f < edges.Length; f++)
            {
                int indexB = edges[f].Other;
                ref var nodeB = ref data.GetNode(indexB);
                float otherDist = nodeB.ShortestColorDistanceAny(color);
                if (otherDist > startDist)
                {
                    ref var otherEnd = ref nodeB.GetEndAny(work.Node);
                    ref var joint = ref data.GetJoint(edges[f].Joint);
                    float lengthB = joint.length + startDist;
                    float strengthB = Mathf.Min(joint.MinLimit, outStrength);
                    
                    if (otherEnd.Out0Root == color)
                    {
                        if (otherEnd.Out0Length == lengthB && otherEnd.Out0Strength == strengthB)
                            continue;
                        otherEnd = ref EnsureWritable(ref otherEnd, work.Node, indexB, ref nodeB);
                        otherEnd.Out0Length = lengthB;
                        otherEnd.Out0Strength = strengthB;
                    } 
                    else if (otherEnd.Out1Root == color)
                    {
                        if (otherEnd.Out1Length == lengthB && otherEnd.Out1Strength == strengthB)
                            continue;
                        otherEnd = ref EnsureWritable(ref otherEnd, work.Node, indexB, ref nodeB);
                        otherEnd.Out1Length = lengthB;
                        otherEnd.Out1Strength = strengthB;
                    }
                    else if (Utils.IsDistanceBetter(lengthB, otherEnd.Out1Length, color, otherEnd.Out1Root))
                    {
                        // napred zkusim horsi hranu
                        otherEnd = ref EnsureWritable(ref otherEnd, work.Node, indexB, ref nodeB);
                        edges[f].In1Root = color;
                        consistencyWorker.MarkDirty(indexB, otherEnd.Out1Root);
                        otherEnd.Out1Root = color;
                        otherEnd.Out1Length = lengthB;
                        otherEnd.Out1Strength = strengthB;
                    }
                    else
                    {
                        continue;
                    }

                    consistencyWorker.MarkDirty(indexB, color);

                    if (otherEnd.In1Root == color || otherEnd.In0Root == color)
                        throw new InvalidOperationException("Pridal jsem barevnou hranu v obou smerech. Cyklus.");

                    if (Utils.IsDistanceBetter(otherEnd.Out1Length, otherEnd.Out0Length, otherEnd.Out1Root, otherEnd.Out0Root))
                    {
                        EdgeEnd.Swap(ref otherEnd, ref edges[f]);
                    }

                    if (lengthB < otherDist)
                    {
                        workQueue.Add(new Work() { Color = color, Length = lengthB, Node = indexB });
                        DeleteOppositeEdges(ref nodeB, indexB, lengthB, work.Node, color);
                    }
                }
            }
        }

        // po pridani hrany jsme zlepsili SCD uzlu. Musime projit vsechny jeho Out hrany a zkontrolovat ze vedou do uzlu s nizsim SCD
        private void DeleteOppositeEdges(ref SpNode node, int nodeIndex, float mySCD, int sourceIndex, int color)
        {
            var edges = node.newEdges;
            for (int f = 0; f < edges.Length; f++)
            {
                int otherIndex = edges[f].Other;
                if (otherIndex == sourceIndex) // obrana abychom si nesmazali hranu delky 0
                    continue;
                if (edges[f].Out0Root == color || edges[f].Out1Root == color)
                {
                    ref var other = ref data.GetNode(otherIndex);
                    float otherSCD = other.ShortestColorDistanceAny(color);
                    if (otherSCD >= mySCD)
                    {
                        toUpdate.Add(otherIndex);
                        other.EnsureNewEdges(data);
                        ref var otherEnd = ref other.GetEndNew(nodeIndex);

                        if (edges[f].Out0Root == color)
                        {
                            EdgeEnd.Delete0(ref edges[f], ref otherEnd);
                        }
                        else
                        {
                            EdgeEnd.Delete1(ref edges[f], ref otherEnd);
                        }

                        // neni potreba volta MarkDirty (nodeIndex, color), protoze uz jsme zavolali drive.
                    }
                }
            }
        }

        private ref EdgeEnd EnsureWritable(ref EdgeEnd otherEnd, int node, int indexB, ref SpNode nodeB)
        {
            if (nodeB.newEdges == null)
            {
                toUpdate.Add(indexB);
                nodeB.EnsureNewEdges(data);
                return ref nodeB.GetEndNew(node);
            }
            else
            {
                return ref otherEnd;
            }
        }
    }
}
