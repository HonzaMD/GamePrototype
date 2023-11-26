using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Core.StaticPhysics
{
    internal class ForceWorker
    {
        private readonly SpDataManager data;
        private readonly HashSet<int> toUpdate;
        private readonly HashSet<int> deletedNodes;
        private readonly BinaryHeap<Work> workQueue = new();
        private readonly Dictionary<int, (int, int)> activeEdges = new();
        private readonly Dictionary<int, (float Damage, int indexA, int indexB)> bigBrokemEdges = new();
        private bool tempPhase;

        private struct Work : IComparable<Work>
        {
            public Vector2 force;
            public float torque;

            public int Node;
            public int Color;
            public float Length;
            public int phase;

            public int CompareTo(Work other)
            {
                int ret = phase - other.phase;
                if (ret == 0)
                {
                    ret = (int)Mathf.Sign(other.Length - Length); // radim sestupne
                    if (ret == 0)
                        ret = Node - other.Node; // zarucim veci ke stejnemu uzlu budou za sebou
                }
                return ret;
            }
        }

        public ForceWorker(SpDataManager data, HashSet<int> toUpdate, HashSet<int> deletedNodes)
        {
            this.data = data;
            this.toUpdate = toUpdate;
            this.deletedNodes = deletedNodes;
        }

        public void RemoveForces()
        {
            tempPhase = false;
            DetectWorkRemove();
            DoWork();
        }

        public void AddForces()
        {
            tempPhase = false;
            DetectWorkAdd();
            DoWork();
        }

        public void AddTempForces(Span<ForceCommand> tempForces)
        {
            tempPhase = true;
            DetectWorkTempForces(tempForces);
            DoWork();
        }

        private void DoWork()
        {
            while (workQueue.Count > 0)
            {
                Update();
            }
        }

        private void DetectWorkRemove()
        {
            foreach (int index in toUpdate)
            {
                ref var node = ref data.GetNode(index);
                if (node.force != Vector2.zero && node.isFixedRoot == 0)
                {
                    var length = node.BestDistance(out var color);
                    if (color == -1)
                        throw new InvalidOperationException("Cekal jsem ze node bude propojeny s nejakym rootem");
                    workQueue.Add(new Work() { Color = color, phase = 0, Length = length, Node = index, force = -node.force });
                }
            }
        }

        private void DetectWorkAdd()
        {
            foreach (int index in toUpdate)
            {
                if (!deletedNodes.Contains(index))
                {
                    ref var node = ref data.GetNode(index);
                    if (node.force != Vector2.zero && node.isFixedRoot == 0)
                    {
                        var length = node.BestDistance(out var color);
                        if (color != -1)
                            workQueue.Add(new Work() { Color = color, phase = 0, Length = length, Node = index, force = node.force });
                    }
                }
            }
        }

        private void DetectWorkTempForces(Span<ForceCommand> tempForces)
        {
            for (int f = 0; f < tempForces.Length; f++)
            {
                var index = tempForces[f].indexA;
                if (!deletedNodes.Contains(index) && EnsureValidNodes(index))
                {
                    ref var node = ref data.GetNode(index);
                    if (node.isFixedRoot == 0)
                    {
                        var length = node.BestDistance(out var color);
                        if (color != -1)
                            workQueue.Add(new Work() { Color = color, phase = 0, Length = length, Node = index, force = tempForces[f].forceA });
                    }
                }
            }
        }

        private bool EnsureValidNodes(int index)
        {
            if (index == 0)
                throw new InvalidOperationException("Zadal jdi sp index 0");

            return data.IsNodeValid(index);
        }



        private void Update()
        {
            var work = workQueue.Remove();
            Accumulate(ref work);

            ref var node = ref data.GetNode(work.Node);

            if (work.phase == 0 && node.FindOtherColor(work.Color, out int color2, out float length2))
            {
                float lenSum = work.Length + length2;
                float w1, w2;
                if (lenSum > 0)
                {
                    w1 = work.Length / lenSum;
                    w2 = length2 / lenSum;
                }
                else
                {
                    w1 = w2 = 0.5f;
                }

                workQueue.Add(new Work() { Color = work.Color, phase = 1, force = work.force * w2, torque = work.torque * w2, Length = work.Length, Node = work.Node });
                workQueue.Add(new Work() { Color = color2, phase = 1, force = work.force * w1, torque = work.torque * w1, Length = length2, Node = work.Node });
            }
            else
            {
                float invLenSum = node.GetInvLenSum(work.Color);
                EdgeEnd[] edges = node.edges;

                for (int f = 0; f < edges.Length; f++)
                {
                    if (edges[f].Out0Root == work.Color)
                        UpdateForce(ref work, invLenSum, edges[f].Out0Lengh, edges[f].Joint, edges[f].Other);
                    if (edges[f].Out1Root == work.Color)
                        UpdateForce(ref work, invLenSum, edges[f].Out1Lengh, edges[f].Joint, edges[f].Other);
                }
            }
        }

        private void UpdateForce(ref Work work, float invLenSum, float length, int jointI, int otherNode)
        {
            if (length == 0)
            {
                UpdateForceZeroLen(ref work, jointI, otherNode);
                return;
            }

            float w = 1 / (length * invLenSum);
            float dir = MathF.Sign(otherNode - work.Node);
            Vector2 force = work.force * (w * dir);
            float torque = work.torque * w;

            ref var joint = ref data.GetJoint(jointI);

            if (activeEdges.TryAdd(jointI, (work.Node, otherNode)))
            {
                joint.tempCompress = 0;
                joint.tempMoment = 0;
            }

            var abf = Vector2.Dot(joint.abDir, force);
            torque += Vector2.Dot(joint.normal, force) * joint.length;

            if (tempPhase)
            {
                joint.tempCompress += abf;
                joint.tempMoment += torque;
            }
            else
            {
                joint.compress += abf;
                joint.moment += torque;
            }

            if (data.GetNode(otherNode).isFixedRoot == 0)
            {
                force *= dir;
                workQueue.Add(new Work() { Color = work.Color, force = force, torque = torque, Length = length - joint.length, Node = otherNode, phase = work.phase });
            }
        }

        private void UpdateForceZeroLen(ref Work work, int jointI, int otherNode)
        {
            ref var joint = ref data.GetJoint(jointI);

            if (activeEdges.TryAdd(jointI, (work.Node, otherNode)))
            {
                joint.tempCompress = 0;
                joint.tempMoment = 0;
            }

            if (tempPhase)
            {
                joint.tempMoment += work.torque;
            }
            else
            {
                joint.moment += work.torque;
            }

            if (data.GetNode(otherNode).isFixedRoot == 0)
            {
                workQueue.Add(new Work() { Color = work.Color, torque = work.torque, Node = otherNode, phase = work.phase });
            }
        }

        private void Accumulate(ref Work work)
        {
            while (workQueue.Count > 0)
            {
                ref var other = ref workQueue.Peek();
                if (other.Node == work.Node && other.phase == work.phase && other.Color == work.Color)
                {
                    work.force += other.force;
                    work.torque += other.torque;
                    workQueue.Remove();
                }
                else
                    break;
            }
        }

        internal void GetBrokenEdges(SpanList<InputCommand> inCommands, SpanList<OutputCommand> outCommands)
        {
            foreach (var pair in activeEdges)
            {
                ref var joint = ref data.GetJoint(pair.Key);
                var compress = joint.compress + joint.tempCompress;
                if (compress > joint.compressLimit || -compress > joint.stretchLimit || MathF.Abs(joint.moment + joint.tempMoment) > joint.momentLimit)
                {
                    inCommands.Add(new InputCommand() 
                    { 
                        Command = SpCommand.RemoveJoint,
                        indexA = pair.Value.Item1,
                        indexB = pair.Value.Item2,
                    });

                    outCommands.Add(new OutputCommand()
                    {
                        Command = SpCommand.RemoveJoint,
                        indexA = pair.Value.Item1,
                        indexB = pair.Value.Item2,
                        nodeA = data.GetNode(pair.Value.Item1).placeable,
                        nodeB = data.GetNode(pair.Value.Item2).placeable,
                    });
                }
            }

            activeEdges.Clear();
        }

        internal void GetBrokenEdgesBigOnly(SpanList<InputCommand> inCommands, SpanList<OutputCommand> outCommands)
        {
            foreach (var pair in activeEdges)
            {
                ref var joint = ref data.GetJoint(pair.Key);
                var compress = joint.compress + joint.tempCompress;
                var moment = MathF.Abs(joint.moment + joint.tempMoment);

                joint.tempCompress = 0; // vynuluju, protoze si muzu nechavat activeEdges do pristiho kola, kde chci tampforces zadat znovu.
                joint.tempMoment = 0;

                var damage = MathF.Max(0, compress - joint.compressLimit) + MathF.Max(0, -compress - joint.stretchLimit) + MathF.Max(0, moment - joint.momentLimit);
                if (damage > 0)
                {
                    ref var endA = ref data.GetNode(pair.Value.Item1).GetEnd(pair.Value.Item2);
                    ref var endB = ref data.GetNode(pair.Value.Item2).GetEnd(pair.Value.Item1);

                    int color = Utils.IsDistanceBetter(endA.Out0Lengh, endB.Out0Lengh, endA.Out0Root, endB.Out0Root) ? endA.Out0Root : endB.Out0Root;

                    if (bigBrokemEdges.TryGetValue(color, out var edge))
                    {
                        if (damage <= edge.Damage)
                            continue;
                    }

                    bigBrokemEdges[color] = (damage, pair.Value.Item1, pair.Value.Item2);
                }
            }

            if (bigBrokemEdges.Count == 0)
            {
                activeEdges.Clear();
            }
            else
            {
                foreach (var edge in bigBrokemEdges.Values)
                {
                    inCommands.Add(new InputCommand()
                    {
                        Command = SpCommand.RemoveJoint,
                        indexA = edge.indexA,
                        indexB = edge.indexB,
                    });

                    outCommands.Add(new OutputCommand()
                    {
                        Command = SpCommand.RemoveJoint,
                        indexA = edge.indexA,
                        indexB = edge.indexB,
                        nodeA = data.GetNode(edge.indexA).placeable,
                        nodeB = data.GetNode(edge.indexB).placeable,
                    });
                }

                bigBrokemEdges.Clear();
            }
        }

        internal void FreeJoint(int index) => activeEdges.Remove(index);
    }
}
