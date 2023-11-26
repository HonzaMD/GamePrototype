using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Core.StaticPhysics
{
    public class GraphWorker
    {
        private readonly SpDataManager data;
        private readonly HashSet<int> toUpdate = new();
        private readonly HashSet<int> deletedNodes = new();
        private readonly HashSet<int> deletedEdges = new();
        private readonly Dictionary<(int, int), int> newEdges = new();
        private readonly DeleteColorWorker deleteColorWorker;
        private readonly AddColorWorker addColorWorker;
        private readonly ForceWorker forceWorker;
        private readonly FindFallenWorker findFallenWorker;

        public GraphWorker(SpDataManager dataManager)
        {
            this.data = dataManager;
            deleteColorWorker = new DeleteColorWorker(data, toUpdate, deletedNodes);
            addColorWorker = new AddColorWorker(data, toUpdate, deletedNodes);
            forceWorker = new ForceWorker(data, toUpdate, deletedNodes);
            findFallenWorker = new FindFallenWorker(data, this, toUpdate, deletedNodes);
        }

        public void ApplyChanges(Span<InputCommand> inputs, Span<ForceCommand> tempForces, SpanList<OutputCommand> output)
        {
            for (int f = 0; f < inputs.Length; f++)
            {
                ref var ic = ref inputs[f];
                EnsureValidNodes(ref ic);

                if (ic.Command == SpCommand.AddNodeAndJoint || ic.Command == SpCommand.AddNode)
                {
                    AddNode(ic);
                }

                if (ic.Command == SpCommand.AddJoint || ic.Command == SpCommand.AddNodeAndJoint)
                {
                    if (!AddJointPrepare(ic, f))
                        ic.ClearAddJoint();
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

            for (int f = 0; f < inputs.Length; f++)
            {
                ref var ic = ref inputs[f];
                if (ic.Command == SpCommand.UpdateForce)
                    toUpdate.Add(ic.indexA);
            }

            forceWorker.RemoveForces();

            for (int f = 0; f < inputs.Length; f++)
            {
                ref var ic = ref inputs[f];
                if (ic.Command is SpCommand.UpdateForce or SpCommand.AddNode or SpCommand.AddNodeAndJoint)
                {
                    UpdateForce(ic);
                }
            }

            foreach (int i in toUpdate)
            {
                ApplyEdgeArrs(i);
            }

            forceWorker.AddForces();
            forceWorker.AddTempForces(tempForces);

            findFallenWorker.Run(output);

            FreeJoints();
            FreeNodes(output);
        }

        private void EnsureValidNodes(ref InputCommand ic)
        {
            if (ic.indexA == 0)
                throw new InvalidOperationException("Zadal jdi sp index 0");

            if (ic.Command != SpCommand.AddNode && ic.Command != SpCommand.AddNodeAndJoint)
            {
                if (!data.IsNodeValid(ic.indexA))
                    ic.Command = SpCommand.None;
            }

            if (ic.indexB != 0)
            {
                if (!data.IsNodeValid(ic.indexB))
                    ic.Command = SpCommand.None;
            }
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
            toUpdate.Add(ic.indexA);
        }

        private bool AddJointPrepare(in InputCommand ic, int icPos)
        {
            if (ic.indexA == ic.indexB)
                throw new InvalidOperationException("Nemuzu vytvorit harnu do sama sebe.");
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
                inputs[icIndex].ClearAddJoint();
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
            node.newEdges = data.GetEdgeArr(node.edges.Length + node.newEdgeCount);

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

        private void ApplyEdgeArrs(int i)
        {
            if (deletedNodes.Contains(i))
                return;

            ref var node = ref data.GetNode(i);

            if (node.newEdges == null)
                return;

            data.ReturnEdgeArr(node.edges);
            node.edges = node.newEdges;
            node.newEdges = null;
            if (node.edges.Length != node.newEdgeCount)
                throw new InvalidOperationException("Divnost, nepridal jsem vsechny hrany");
            node.newEdgeCount = 0;
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
            var diffV = ic.indexA < ic.indexB ? nodeB.position - nodeA.position : nodeA.position - nodeB.position;
            joint.length = diffV.magnitude;
            joint.abDir = joint.length > 0.001f ? diffV / joint.length : Vector2.zero;
            joint.stretchLimit = ic.stretchLimit;
            joint.compressLimit = ic.compressLimit;
            joint.momentLimit = ic.momentLimit;

            edgeA.Joint = jointIndex;
            edgeA.Other = ic.indexB;

            edgeB.Joint = jointIndex;
            edgeB.Other = ic.indexA;
        }

        private void UpdateForce(in InputCommand ic)
        {
            ref var node = ref data.GetNode(ic.indexA);
            node.force += ic.forceA;
        }

        public void GetBrokenEdges(SpanList<InputCommand> inCommands, SpanList<OutputCommand> outCommands) => forceWorker.GetBrokenEdges(inCommands, outCommands);
        public void GetBrokenEdgesBigOnly(SpanList<InputCommand> inCommands, SpanList<OutputCommand> outCommands) => forceWorker.GetBrokenEdgesBigOnly(inCommands, outCommands);

        private void FreeJoints()
        {
            foreach (int index in deletedEdges)
            {
                FreeJoint(index);
            }
            deletedEdges.Clear();
        }

        internal void FreeJoint(int index)
        {
            data.FreeJoint(index);
            forceWorker.FreeJoint(index);
        }

        private void FreeNodes(SpanList<OutputCommand> output)
        {
            foreach (int index in deletedNodes)
            {
                data.ClearNode(index);
                output.Add(new OutputCommand() { Command = SpCommand.FreeNode, indexA = index });
            }
            deletedNodes.Clear();
        }
    }
}
