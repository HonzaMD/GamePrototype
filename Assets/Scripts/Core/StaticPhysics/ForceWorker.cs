﻿using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Core.StaticPhysics
{
    internal class ForceWorker
    {
        private readonly SpDataManager data;
        private readonly HashSet<int> toUpdate;
        private readonly HashSet<int> deletedNodes;
        private readonly BinaryHeap<Work> workQueue = new BinaryHeap<Work>();

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
            DetectWorkRemove();
            while (workQueue.Count > 0)
            {
                Update();
            }
        }

        internal void AddForces()
        {
            DetectWorkAdd();
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

        private void Update()
        {
            var work = workQueue.Remove();
            Accumulate(ref work);

            ref var node = ref data.GetNode(work.Node);

            if (work.phase == 0 && node.FindOtherColor(work.Color, out int color2, out float length2, false))
            {
                float lenSum = work.Length + length2;
                float w1 = work.Length / lenSum;
                float w2 = length2 / lenSum;

                workQueue.Add(new Work() { Color = work.Color, phase = 1, force = work.force * w2, torque = work.torque * w2, Length = work.Length, Node = work.Node });
                workQueue.Add(new Work() { Color = color2, phase = 1, force = work.force * w1, torque = work.torque * w1, Length = length2, Node = work.Node });
            }
            else
            {
                float lenSum = node.GetLenSum(work.Color, false);
                EdgeEnd[] edges = node.edges;

                for (int f = 0; f < edges.Length; f++)
                {
                    if (edges[f].Out0Root == work.Color)
                        UpdateForce(ref work, lenSum, edges[f].Out0Lengh, edges[f].Joint, edges[f].Other);
                    if (edges[f].Out1Root == work.Color)
                        UpdateForce(ref work, lenSum, edges[f].Out1Lengh, edges[f].Joint, edges[f].Other);
                }
            }
        }

        private void UpdateForce(ref Work work, float lenSum, float length, int joint, int otherNode)
        {
            float w = 1 - length / lenSum;
            float dir = MathF.Sign(otherNode - work.Node);
            w *= dir;
            Vector2 force = work.force * w;
            float torque = work.torque * w;

            ref var j = ref data.GetJoint(joint);
            var abf = Vector2.Dot(j.abDir, force);
            j.compress += abf;
            torque += Vector2.Dot(j.normal, force) * j.length;
            j.moment += torque;

            if (data.GetNode(otherNode).isFixedRoot == 0)
            {
                force *= dir;
                torque *= dir;

                workQueue.Add(new Work() { Color = work.Color, force = force, torque = torque, Length = length - j.length, Node = otherNode, phase = work.phase });
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
    }
}
