using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Core.StaticPhysics
{
    internal class GraphWorker
    {
        private readonly SpDataManager data;
        private readonly HashSet<int> toUpdate = new HashSet<int>();
        private readonly HashSet<int> deletedNodes = new HashSet<int>();
        private readonly HashSet<int> deletedEdges = new HashSet<int>();
        private readonly Dictionary<(int, int), int> newEdges = new Dictionary<(int, int), int>();
        private readonly DeleteColorWorker deleteColorWorker;
        private readonly AddColorWorker addColorWorker;

        public GraphWorker(SpDataManager dataManager)
        {
            this.data = dataManager;
            deleteColorWorker = new DeleteColorWorker(data, toUpdate, deletedNodes);
            addColorWorker = new AddColorWorker(data, toUpdate, deletedNodes);
        }

        public void ApplyChanges(Span<InputCommand> inputs)
        {
            for (int f = 0; f < inputs.Length; f++)
            {
                ref var ic = ref inputs[f];
                if (ic.Command == SpCommand.AddNodeAndJoint || ic.Command == SpCommand.AddNode)
                {
                    AddNode(ic);
                }

                if (ic.Command == SpCommand.AddJoint || ic.Command == SpCommand.AddNodeAndJoint)
                {
                    if (!AddJointPrepare(ic, f))
                        ic.Command = SpCommand.None;
                }

                if (ic.Command == SpCommand.RemoveJoint)
                {
                    if (!RemoveJointPrepare(ic, inputs))
                        ic.Command = SpCommand.None;
                }

                if (ic.Command == SpCommand.RemoveNode)
                {
                    RemoveNode(ic);
                }
            }

            foreach (int i in toUpdate)
            {
                CreateNewEdgeArrs(i);
            }

            for (int f = 0; f < inputs.Length; f++)
            {
                ref var ic = ref inputs[f];
                if (ic.Command == SpCommand.AddJoint || ic.Command == SpCommand.AddNodeAndJoint)
                {
                    AddJoint(ic);
                }
            }

            newEdges.Clear();
            deleteColorWorker.Run();
            addColorWorker.Run();
        }


        private void AddNode(in InputCommand ic)
        {
            ref var node = ref data.AddOrGetNode(ic.indexA);
            if (node.edges != null)
                throw new InvalidOperationException("Cekal jsem novy node");
            node.placeable = ic.nodeA;
            node.isFixedRoot = ic.isAFixed ? ic.indexA : 0;
            node.position = ic.pointA;
            node.edges = data.GetEdgeArr(0);
            node.force = ic.forceA;
            toUpdate.Add(ic.indexA);
        }

        private bool AddJointPrepare(in InputCommand ic, int icPos)
        {
            ref var nodeA = ref data.GetNode(ic.indexA);
            ref var nodeB = ref data.GetNode(ic.indexB);
            if (deletedNodes.Contains(ic.indexA) || deletedNodes.Contains(ic.indexB))
                return false;
            if (nodeA.ConnectsTo(ic.indexB))
                return false;
            if (newEdges.ContainsKey(ic.EdgePairId))
                return false;
            nodeA.newEdgeCount++;
            nodeB.newEdgeCount++;
            toUpdate.Add(ic.indexA);
            toUpdate.Add(ic.indexB);
            newEdges.Add(ic.EdgePairId, icPos);
            return true;
        }

        private bool RemoveJointPrepare(in InputCommand ic, Span<InputCommand> inputs)
        {
            ref var nodeA = ref data.GetNode(ic.indexA);
            ref var nodeB = ref data.GetNode(ic.indexB);
            if (deletedNodes.Contains(ic.indexA) || deletedNodes.Contains(ic.indexB))
                return false;
            if (nodeA.ConnectsTo(ic.indexB, out var joint))
            {
                if (deletedEdges.Add(joint))
                {
                    nodeA.newEdgeCount--;
                    nodeB.newEdgeCount--;
                    toUpdate.Add(ic.indexA);
                    toUpdate.Add(ic.indexB);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else if (newEdges.TryGetValue(ic.EdgePairId, out var icIndex))
            {
                newEdges.Remove(ic.EdgePairId);
                inputs[icIndex].Command = SpCommand.None;
                nodeA.newEdgeCount--;
                nodeB.newEdgeCount--;
                return false;
            }
            else
            {
                return false;
            }
        }

        private void RemoveNode(in InputCommand ic)
        {
            if (deletedNodes.Contains(ic.indexA))
                return;
            deletedNodes.Add(ic.indexA);
            toUpdate.Add(ic.indexA);
            ref var node = ref data.GetNode(ic.indexA);

            for (int f = 0; f < node.edges.Length; f++)
            {
                if (deletedEdges.Add(node.edges[f].Joint))
                {
                    int indexB = node.edges[f].Other;
                    ref var nodeB = ref data.GetNode(indexB);
                    nodeB.newEdgeCount--;
                    toUpdate.Add(indexB);
                }
            }
        }

        private void CreateNewEdgeArrs(int i)
        {
            if (deletedNodes.Contains(i))
                return;

            ref var node = ref data.GetNode(i);
            node.newEdges = data.GetEdgeArr(node.newEdgeCount);

            int count = 0;
            for (int f = 0; f < node.edges.Length; f++)
            {
                if (!deletedEdges.Contains(node.edges[f].Joint))
                {
                    node.newEdges[count] = node.edges[f];
                    count++;
                }
            }
            node.newEdgeCount = count;
        }

        private void AddJoint(in InputCommand ic)
        {
            ref var nodeA = ref data.GetNode(ic.indexA);
            ref var nodeB = ref data.GetNode(ic.indexB);
            if (deletedNodes.Contains(ic.indexA) || deletedNodes.Contains(ic.indexB))
                return;

            ref var edgeA = ref nodeA.newEdges[nodeA.newEdgeCount];
            ref var edgeB = ref nodeB.newEdges[nodeB.newEdgeCount];
            nodeA.newEdgeCount++;
            nodeB.newEdgeCount++;

            ref var joint = ref data.AddJoint(out int jointIndex);
            joint.length = (nodeA.position - nodeB.position).magnitude;
            joint.stretchLimit = ic.stretchLimit;
            joint.compressLimit = ic.compressLimit;
            joint.momentLimit = ic.momentLimit;

            edgeA.Joint = jointIndex;
            edgeA.Other = ic.indexB;

            edgeB.Joint = jointIndex;
            edgeB.Other = ic.indexA;
        }
    }
}
