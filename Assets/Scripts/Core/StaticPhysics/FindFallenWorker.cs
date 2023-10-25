using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Core.StaticPhysics
{
    internal class FindFallenWorker
    {
        private readonly SpDataManager data;
        private readonly HashSet<int> toUpdate;
        private readonly HashSet<int> deletedNodes;
        private readonly Queue<int> workQueue = new();
        private readonly List<(int Index, int NodeA, int NodeB)> edgesToFall = new();
        private SpanList<OutputCommand> output;

        public FindFallenWorker(SpDataManager data, HashSet<int> toUpdate, HashSet<int> deletedNodes)
        {
            this.data = data;
            this.toUpdate = toUpdate;
            this.deletedNodes = deletedNodes;
        }

        internal void Run(SpanList<OutputCommand> output)
        {
            this.output = output;
            int outputInitialSize = output.Count;

            while (toUpdate.Count > 0)
            {
                int index = PopUpdateSet();
                if (!deletedNodes.Contains(index))
                {
                    ref var node = ref data.GetNode(index);
                    if (!node.IsConnectedToRoot())
                    {
                        CollectNode(index, ref node);
                        CollectMoreNodes();
                    }
                }
            }

            int outputAfterNodesSize = output.Count;
            OutputEdges();
            FreeNodes(output.AsSpan(outputInitialSize, outputAfterNodesSize - outputInitialSize));
            this.output = null;
        }

        private void CollectMoreNodes()
        {
            while (workQueue.Count > 0)
            {
                int index = workQueue.Dequeue();
                CollectNode(index, ref data.GetNode(index));
            }
        }

        private void CollectNode(int index, ref SpNode node)
        {
            for (int f = 0; f < node.edges.Length; f++)
            {
                int indexB = node.edges[f].Other;
                if (toUpdate.Remove(indexB) && !deletedNodes.Contains(indexB))
                {
                    workQueue.Enqueue(indexB);
                    edgesToFall.Add((node.edges[f].Joint, index, indexB));
                }
            }
            output.Add(new OutputCommand()
            {
                Command = SpCommand.FallNode,
                indexA = index,
                nodeA = node.placeable
            });
        }

        private int PopUpdateSet()
        {
            var e = toUpdate.GetEnumerator();
            e.MoveNext();
            int index = e.Current;
            toUpdate.Remove(index);
            return index;
        }

        private void OutputEdges()
        {
            foreach (var edge in edgesToFall)
            {
                ref var joint = ref data.GetJoint(edge.Index);
                output.Add(new OutputCommand()
                {
                    Command = SpCommand.FallEdge,
                    indexA = edge.NodeA,
                    indexB = edge.NodeB,
                    nodeA = data.GetNode(edge.NodeA).placeable,
                    nodeB = data.GetNode(edge.NodeB).placeable,
                    compressLimit = joint.compressLimit,
                    stretchLimit = joint.stretchLimit,
                    momentLimit = joint.momentLimit,
                });
                data.FreeJoint(edge.Index);
            }
            edgesToFall.Clear();
        }

        private void FreeNodes(Span<OutputCommand> fallNodes)
        {
            for (int f = 0; f < fallNodes.Length; f++)
            {
                if (fallNodes[f].Command != SpCommand.FallNode)
                    throw new InvalidOperationException("Cekal jsem jen SpCommand.FallNode");
                data.ClearNode(fallNodes[f].indexA);
            }
        }
    }
}
