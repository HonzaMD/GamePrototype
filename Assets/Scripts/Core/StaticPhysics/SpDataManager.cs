using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        private const int edgePoolMax = 10;
        private readonly Stack<EdgeEnd[]>[] edgeEndsPool = Enumerable.Range(0, edgePoolMax).Select(i => new Stack<EdgeEnd[]>()).ToArray();

        // volano z threadu hry
        public int ReserveNodeIndex() 
        {
            if (freeNodeIndexes.Count > 0)
                return freeNodeIndexes.Pop();
            topNodeIndex++;
            return topNodeIndex;
        }

        // volano z threadu hry
        public void FreeNodeIndex(int index)
        {
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
    }
}
