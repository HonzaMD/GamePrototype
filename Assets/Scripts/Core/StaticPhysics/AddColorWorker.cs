﻿using Assets.Scripts.Utils;
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
        private readonly BinaryHeap<Work> workQueue = new BinaryHeap<Work>();

        private struct Work : IComparable<Work>
        {
            public int Node;
            public int Color;
            public float Length;

            public int CompareTo(Work other) => (int)Mathf.Sign(Length - other.Length);
        }

        public AddColorWorker(SpDataManager data, HashSet<int> toUpdate, HashSet<int> deletedNodes)
        {
            this.data = data;
            this.toUpdate = toUpdate;
            this.deletedNodes = deletedNodes;
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
            if (IsColorValid(color, node.newEdges, f) && CanExtend(node, color, out float startDist))
                workQueue.Add(new Work() { Color = color, Length = startDist, Node = index });
        }

        private bool IsColorValid(int color, EdgeEnd[] newEdges, int f)
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

            var edges = node.newEdges;
            for (int f = 0; f < edges.Length; f++)
            {
                int indexB = edges[f].Other;
                ref var nodeB = ref data.GetNode(indexB);
                float otherDist = nodeB.ShortestColorDistanceAny(color);
                if (otherDist > startDist)
                {
                    ref var otherEnd = ref nodeB.GetEndAny(work.Node);
                    float lengthB = data.GetJoint(edges[f].Joint).length + startDist;
                    
                    if (otherEnd.Out0Root == color)
                    {
                        if (otherEnd.Out0Lengh == lengthB)
                            continue;
                        otherEnd = ref EnsureWritable(ref otherEnd, work.Node, indexB, ref nodeB);
                        otherEnd.Out0Lengh = lengthB;
                    } 
                    else if (otherEnd.Out1Root == color)
                    {
                        if (otherEnd.Out1Lengh == lengthB)
                            continue;
                        otherEnd = ref EnsureWritable(ref otherEnd, work.Node, indexB, ref nodeB);
                        otherEnd.Out1Lengh = lengthB;
                    }
                    else if (Utils.IsDistanceBetter(lengthB, otherEnd.Out1Lengh, color, otherEnd.Out1Root))
                    {
                        // napred zkusim horsi hranu
                        otherEnd = ref EnsureWritable(ref otherEnd, work.Node, indexB, ref nodeB);
                        edges[f].In1Root = color;
                        otherEnd.Out1Root = color;
                        otherEnd.Out1Lengh = lengthB;
                    }
                    else
                    {
                        continue;
                    }

                    if (Utils.IsDistanceBetter(otherEnd.Out1Lengh, otherEnd.Out0Lengh, otherEnd.Out1Root, otherEnd.Out0Root))
                    {
                        // swap
                        float tempLen = otherEnd.Out0Lengh;
                        int tempRoot = otherEnd.Out0Root;
                        otherEnd.Out0Lengh = otherEnd.Out1Lengh;
                        otherEnd.Out0Root = otherEnd.Out1Root;
                        edges[f].In0Root = otherEnd.Out1Root;
                        otherEnd.Out1Lengh = tempLen;
                        otherEnd.Out1Root = tempRoot;
                        edges[f].In1Root = tempRoot;
                    }

                    if (lengthB < otherDist)
                        workQueue.Add(new Work() { Color = color, Length = lengthB, Node = indexB });
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
