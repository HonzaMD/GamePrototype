using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Assertions;

namespace Assets.Scripts.Core.StaticPhysics
{
    // Po DeleteColor/AddColor (a UpdateJointLimits) zustanou v grafu zastarale Strength hodnoty
    // na Out hranach a list-side uzly nemusi vedet, ze se jejich force routing zmenil.
    // ConsistencyWorker oba problemy vyresi v jedinem behu:
    //   Phase 1: BFS dolu (z dirty seedu po In hranach v dane barve) -> kazdy navstiveny
    //            uzel jde do toUpdate (jeho force routing pres tuto barvu se mohl zmenit).
    //   Phase 2: Kahn topo sort v ramci kandidatu nahoru pres Out hrany; pri zpracovani
    //            kazdeho uzlu prepocitam Out{0,1}Strength = Min(joint.MinLimit, neighbor.MaxOutStrength).
    //
    // Volajici (AddColorWorker, DeleteColorWorker, UpdateJointLimits) vola MarkDirty(node, color)
    // pri kazde zmene Out{0,1}* dat. Seeds se shromazduji v HashSetu (idempotentni pri opakovanem
    // volani).
    internal class ConsistencyWorker
    {
        private readonly SpDataManager data;
        private readonly HashSet<int> toUpdate;

        private readonly HashSet<long> candidates = new();
        private readonly Queue<long> queue = new();
        private readonly Dictionary<long, int> inDegree = new();
        private readonly Predicate<long> keyIsDeleted;

        public ConsistencyWorker(SpDataManager data, HashSet<int> toUpdate, HashSet<int> deletedNodes)
        {
            this.data = data;
            this.toUpdate = toUpdate;
            keyIsDeleted = key => deletedNodes.Contains(KeyNode(key));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long Key(int node, int color) => ((long)node << 32) | (uint)color;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int KeyNode(long key) => (int)(key >> 32);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int KeyColor(long key) => (int)key;

        public void MarkDirty(int node, int color)
        {
            if (color == 0) return;
            candidates.Add(Key(node, color));
        }

        public void Run()
        {
            candidates.RemoveWhere(keyIsDeleted);

            BfsDownAndSeedToUpdate();
            ComputeInDegrees();
            ProcessTopo();

            candidates.Clear();
        }

        // Phase 1: prosadi BFS z kazdeho seedu po In hranach v dane barve.
        // Vsechny navstivene uzly jsou dirty -> hned do toUpdate.
        private void BfsDownAndSeedToUpdate()
        {
            foreach (long s in candidates)
                queue.Enqueue(s);

            while (queue.Count > 0)
            {
                long key = queue.Dequeue();

                int nodeIdx = KeyNode(key);
                int color = KeyColor(key);

                toUpdate.Add(nodeIdx);

                ref var node = ref data.GetNode(nodeIdx);
                var edges = node.newEdges ?? node.edges;

                for (int f = 0; f < edges.Length; f++)
                {
                    if (edges[f].In0Root == color || edges[f].In1Root == color)
                    {
                        long nKey = Key(edges[f].Other, color);
                        if (candidates.Add(nKey))
                            queue.Enqueue(nKey);
                    }
                }
            }
        }

        // Pro kazdeho kandidata: pocet Out hran teto barvy vedoucich do jineho kandidata.
        // Pokud 0 -> jeho upstream je uz finalni (nebyl dirty) -> muze zacit zpracovani.
        private void ComputeInDegrees()
        {
            foreach (long key in candidates)
            {
                int nodeIdx = KeyNode(key);
                int color = KeyColor(key);

                ref var node = ref data.GetNode(nodeIdx);
                var edges = node.newEdges ?? node.edges;

                int deg = 0;
                for (int f = 0; f < edges.Length; f++)
                {
                    if ((edges[f].Out0Root == color || edges[f].Out1Root == color) && candidates.Contains(Key(edges[f].Other, color)))
                    {
                        deg++;
                        Assert.IsTrue(edges[f].Out0Root != edges[f].Out1Root, "Barva nesmi tect zaroven pres hranu 0 a hranu 1");
                    }
                }

                if (deg == 0)
                    queue.Enqueue(key);
                else
                    inDegree.Add(key, deg);
            }
        }

        // Kahn: zpracuj ready frontu, prepocitej Strength, dekrementuj nasledniky (In neighbors).
        private void ProcessTopo()
        {
            while (queue.Count > 0)
            {
                long key = queue.Dequeue();
                int nodeIdx = KeyNode(key);
                int color = KeyColor(key);

                ref var node = ref data.GetNode(nodeIdx);
                var edges = node.newEdges ?? node.edges;

                float outStr = node.FindOutStrengthAny(color);
                float scd = node.ShortestColorDistanceAny(color);

                for (int f = 0; f < edges.Length; f++)
                {
                    if (edges[f].In0Root == color)
                    {
                        Assert.IsTrue(scd != float.MaxValue, "hrana bez korenu");
                        int indexB = edges[f].Other;
                        ref var nodeB = ref data.GetNode(indexB);
                        ref var otherEnd = ref nodeB.GetEndAny(nodeIdx);
                        ref var joint = ref data.GetJoint(edges[f].Joint);
                        float lengthB = joint.length + scd;
                        float strengthB = Mathf.Min(joint.MinLimit, outStr);

                        Assert.AreEqual(lengthB, otherEnd.Out0Length, "hrana ma spatnouy LENGTH");
                        if (otherEnd.Out0Length == lengthB && otherEnd.Out0Strength == strengthB)
                            continue;
                        ref var otherEnd2 = ref EnsureWritable(ref otherEnd, nodeIdx, ref node, ref nodeB, out edges);
                        otherEnd2.Out0Length = lengthB;
                        otherEnd2.Out0Strength = strengthB;
                    }
                    else if (edges[f].In1Root == color)
                    {
                        Assert.IsTrue(scd != float.MaxValue, "hrana bez korenu");
                        int indexB = edges[f].Other;
                        ref var nodeB = ref data.GetNode(indexB);
                        ref var otherEnd = ref nodeB.GetEndAny(nodeIdx);
                        ref var joint = ref data.GetJoint(edges[f].Joint);
                        float lengthB = joint.length + scd;
                        float strengthB = Mathf.Min(joint.MinLimit, outStr);

                        Assert.AreEqual(lengthB, otherEnd.Out1Length, "hrana ma spatnouy LENGTH");
                        if (otherEnd.Out1Length == lengthB && otherEnd.Out1Strength == strengthB)
                            continue;
                        ref var otherEnd2 = ref EnsureWritable(ref otherEnd, nodeIdx, ref node, ref nodeB, out edges);
                        otherEnd2.Out1Length = lengthB;
                        otherEnd2.Out1Strength = strengthB;
                    }
                }


                // Otevri In sousedy (downstream kandidaty)
                for (int f = 0; f < edges.Length; f++)
                {
                    if (edges[f].In0Root == color || edges[f].In1Root == color)
                    {
                        long nKey = Key(edges[f].Other, color);
                        if (inDegree.TryGetValue(nKey, out int deg))
                        {
                            if (--deg == 0)
                            {
                                inDegree.Remove(nKey);
                                queue.Enqueue(nKey);
                            }
                            else
                            {
                                inDegree[nKey] = deg;
                            }
                        }
                    }
                }
            }

            if (inDegree.Count > 0)
                throw new InvalidOperationException("Cyklus v barevnem grafu");
        }


        private ref EdgeEnd EnsureWritable(ref EdgeEnd otherEnd, int nodeIdx, ref SpNode node, ref SpNode nodeB, out EdgeEnd[] edges)
        {
            node.EnsureNewEdges(data);
            edges = node.newEdges;

            if (nodeB.newEdges == null)
            {
                nodeB.EnsureNewEdges(data);
                return ref nodeB.GetEndNew(nodeIdx);
            }
            else
            {
                return ref otherEnd;
            }
        }
    }
}
