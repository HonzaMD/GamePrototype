using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Core.StaticPhysics
{
    public enum SpCommand
    {
        None,
        AddJoint,
        AddNode,
        AddNodeAndJoint,
        RemoveJoint,
        RemoveNode,
        BatchEnd,

        FallEdge,
        FallNode,
        FreeNode,
    }

    public struct InputCommand
    {
        public SpCommand Command;
        public int indexA;
        public int indexB;
        public Placeable nodeA;
        public Vector2 forceA;
        public Vector2 pointA;
        public bool isAFixed;
        public float stretchLimit;
        public float compressLimit;
        public float momentLimit;

        public readonly (int, int) EdgePairId => indexA < indexB ? (indexA, indexB) : (indexB, indexA);
    }

    public struct OutputCommand
    {
        public SpCommand Command;
        public int indexA;
        public int indexB;
        public Placeable nodeA;
        public Placeable nodeB;
        public float stretchLimit;
        public float compressLimit;
        public float momentLimit;
    }
}