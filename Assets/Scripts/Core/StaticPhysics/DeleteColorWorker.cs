using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Core.StaticPhysics
{
    internal class DeleteColorWorker
    {
        private readonly SpDataManager data;
        private readonly HashSet<int> toUpdate;
        private readonly HashSet<int> deletedNodes;
        private readonly Queue<Work> workQueue = new Queue<Work>();

        private struct Work
        {
            public int Node;
            public int Color;
        }

        public DeleteColorWorker(SpDataManager data, HashSet<int> toUpdate, HashSet<int> deletedNodes)
        {
            this.data = data;
            this.toUpdate = toUpdate;
            this.deletedNodes = deletedNodes;
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
                bool delete = false;
                if (node.newEdges[f].In0Root == color)
                {
                    delete = true;
                    node.newEdges[f].In0Root = 0;
                }
                if (node.newEdges[f].In1Root == color)
                {
                    delete = true;
                    node.newEdges[f].In1Root = 0;
                }

                if (delete)
                {
                    DeleteOtherEdge(color, node.newEdges[f].Other, index);
                }
            }
        }

        private void DeleteOtherEdge(int color, int index, int from)
        {
            toUpdate.Add(index);
            ref var node = ref data.GetNode(index);
            ref var edge = ref node.GetEnd(from);
            if (edge.Out0Root == color)
            {
                edge.Out0Root = 0;
                edge.Out0Lengh = 0;
            }
            if (edge.Out1Root == color)
            {
                edge.Out1Root = 0;
                edge.Out1Lengh = 0;
            }
            workQueue.Enqueue(new Work() { Node = index, Color = color });
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
            if (IsColorValid(color, node.newEdges, f) && node.ShortestColorDistance(color) != node.ShortestColorDistanceNew(color))
                workQueue.Enqueue(new Work() { Node = index, Color = color });
        }

        private bool IsColorValid(int color, EdgeEnd[] newEdges, int f)
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
