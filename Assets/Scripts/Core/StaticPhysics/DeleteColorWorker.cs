using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Core.StaticPhysics
{
    // Promaze nevalidni barevne hrany
    // Nevalidni barevna hrana:
    // proverim vsechny vstupujici barvy. Pokud se na uzlu zmenilo ShortestColorDistance, 
    // musim vstupujici barevnou hranu odstranit + rekurzivne provedu u vsech vrcholu kam hrany vedou
    // 


    internal class DeleteColorWorker
    {
        private readonly SpDataManager data;
        private readonly HashSet<int> toUpdate;
        private readonly HashSet<int> deletedNodes;
        private readonly ConsistencyWorker consistencyWorker;
        private readonly Queue<Work> workQueue = new Queue<Work>();

        private struct Work
        {
            public int Node;
            public int Color;
        }

        public DeleteColorWorker(SpDataManager data, HashSet<int> toUpdate, HashSet<int> deletedNodes, ConsistencyWorker consistencyWorker)
        {
            this.data = data;
            this.toUpdate = toUpdate;
            this.deletedNodes = deletedNodes;
            this.consistencyWorker = consistencyWorker;
        }

        internal void Run()
        {
            DetectChanges();
            while (workQueue.Count > 0)
            {
                var work = workQueue.Dequeue();
                DeleteColor(work.Color, work.Node);
            }
        }

        private void DeleteColor(int color, int index)
        {
            ref var node = ref data.GetNode(index);
            for (int f = 0; f < node.newEdges.Length; f++)
            {
                if (node.newEdges[f].In0Root == color)
                {
                    ref var outEdge = ref DeleteOtherEdge(color, node.newEdges[f].Other, index);
                    EdgeEnd.Delete0(ref outEdge, ref node.newEdges[f]);
                }
                else if (node.newEdges[f].In1Root == color)
                {
                    ref var outEdge = ref DeleteOtherEdge(color, node.newEdges[f].Other, index);
                    EdgeEnd.Delete1(ref outEdge, ref node.newEdges[f]);
                }
            }
        }

        private ref EdgeEnd DeleteOtherEdge(int color, int index, int from)
        {
            toUpdate.Add(index);
            ref var node = ref data.GetNode(index);
            node.EnsureNewEdges(data);
            ref var edge = ref node.GetEndNew(from);
            workQueue.Enqueue(new Work() { Node = index, Color = color });
            //consistencyWorker.MarkDirty(index, color); Neni potreba volat protoze z uzlu smazu vsechny In Hrany a uzel davam do ToUpdate (Uzel bude bud listem nebo barvu nebude mit vubec)
            return ref edge;
        }

        private void DetectChanges()
        {
            foreach (int index in toUpdate)
            {
                if (!deletedNodes.Contains(index))
                {
                    ref var node = ref data.GetNode(index);

                    for (int f = 0; f < node.newEdges.Length; f++)
                    {
                        TestColorCandidate(index, node, f, node.newEdges[f].In0Root);
                        TestColorCandidate(index, node, f, node.newEdges[f].In1Root);
                    }
                }
            }
        }

        private void TestColorCandidate(int index, in SpNode node, int f, int color)
        {
            if (IsFirstColor(color, node.newEdges, f) && node.ShortestColorDistance(color) != node.ShortestColorDistanceNew(color))
                workQueue.Enqueue(new Work() { Node = index, Color = color });
        }

        private bool IsFirstColor(int color, EdgeEnd[] newEdges, int f)
        {
            if (color == 0)
                return false;
            for (int i = 0; i < f; i++)
            {
                if (newEdges[i].In0Root == color)
                    return false;
                if (newEdges[i].In1Root == color)
                    return false;
            }
            return true;
        }
    }
}
