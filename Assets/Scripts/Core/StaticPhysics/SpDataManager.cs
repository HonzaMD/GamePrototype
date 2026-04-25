using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Core.StaticPhysics
{
    public class SpDataManager
    {
        private SpNode[] nodes = new SpNode[256];
        private SpJoint[] joints = new SpJoint[256];
        private readonly Stack<int> freeNodeIndexes = new Stack<int>();
        private readonly Stack<int> freeJointIndexes = new Stack<int>();
        private int topNodeIndex; // prideluji od 1
        private int topJointIndex; // prideluji od 0

        private const int edgePoolMax = 25;
        private readonly Stack<EdgeEnd[]>[] edgeEndsPool = Enumerable.Range(0, edgePoolMax).Select(i => new Stack<EdgeEnd[]>()).ToArray();

        // zachyceno v konstruktoru na game threadu (SpInterface je vytvoren z Game.cs)
        private readonly int gameThreadId = Thread.CurrentThread.ManagedThreadId;

        [System.Diagnostics.Conditional("UNITY_ASSERTIONS")]
        private void AssertGameThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != gameThreadId)
                throw new InvalidOperationException("SpDataManager: ReserveNodeIndex/FreeNodeIndex musi byt volano z game threadu");
        }

        // volano z threadu hry
        public int ReserveNodeIndex()
        {
            AssertGameThread();
            if (freeNodeIndexes.Count > 0)
                return freeNodeIndexes.Pop();
            topNodeIndex++;
            return topNodeIndex;
        }

        // volano z threadu hry
        public void FreeNodeIndex(int index)
        {
            AssertGameThread();
            freeNodeIndexes.Push(index);
        }

        internal ref SpNode GetNode(int index) => ref nodes[index];
        internal ref SpNode AddOrGetNode(int index)
        {
            if (index >= nodes.Length)
                Array.Resize(ref nodes, Math.Max(index + 1,  nodes.Length * 2));
            return ref nodes[index];
        }

        public ref SpJoint GetJoint(int index) => ref joints[index];
        internal ref SpJoint AddJoint(out int index)
        {
            if (freeJointIndexes.Count > 0)
            {
                index = freeJointIndexes.Pop();
            }
            else
            {
                index = topJointIndex;
                topJointIndex++;
            }

            if (index >= joints.Length)
                Array.Resize(ref joints, Math.Max(index + 1, joints.Length * 2));

            return ref joints[index];
        }

        internal void FreeJoint(int index)
        {
            joints[index] = default;
            freeJointIndexes.Push(index);
        }

        internal EdgeEnd[] GetEdgeArr(int size)
        {
            if (size == 0)
                return Array.Empty<EdgeEnd>();
            return (size > edgePoolMax || edgeEndsPool[size - 1].Count == 0) ? new EdgeEnd[size] : edgeEndsPool[size - 1].Pop();
        }

        internal void ClearNode(int index)
        {
            ref var node = ref GetNode(index);
            if (node.edges != null)
                ReturnEdgeArr(node.edges);
            if (node.newEdges != null)
                ReturnEdgeArr(node.newEdges);
            node = default;
        }

        internal void ReturnEdgeArr(EdgeEnd[] arr)
        {
            if (arr.Length > 0 && arr.Length <= edgePoolMax)
            {
                Array.Clear(arr, 0, arr.Length);
                edgeEndsPool[arr.Length - 1].Push(arr);
            }
        }

        internal bool IsNodeValid(int index) => index < nodes.Length && nodes[index].edges != null;

        // Pro testy: vrati Out0Root a Out1Root hrany z 'from' do 'to'. Hrana musi existovat.
        public (int Out0Root, int Out1Root) GetOutRoots(int from, int to)
        {
            ref var node = ref nodes[from];
            if (node.edges == null)
                throw new InvalidOperationException($"Uzel {from} neexistuje");
            for (int f = 0; f < node.edges.Length; f++)
            {
                if (node.edges[f].Other == to)
                    return (node.edges[f].Out0Root, node.edges[f].Out1Root);
            }
            throw new InvalidOperationException($"Hrana {from}->{to} neni");
        }
    }
}
