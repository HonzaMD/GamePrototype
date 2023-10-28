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
        RemoveNode, // i vystup, dostanes, kdyz hrana rupne
        UpdateForce,  

        FallNode,  // vystup - uzel ktery neni k nicemu prichycen a ma zacit padat
        FallEdge,  // hrany mezi padajicimi uzly
        FreeNode,  // po zpracovani RemoveNode, dostanes FreeNode, abys uvolnil index vyrazeneho nodu
    }

    public struct InputCommand
    {
        public SpCommand Command;
        public int indexA;
        public int indexB;      // nutne pro praci s hranami
        public Placeable nodeA; // pro AddNode
        public Vector2 forceA;  // reletivni zmena sily, pro AddNode a UpdateForce
        public Vector2 pointA;  // pro AddNode
        public bool isAFixed;   // pro AddNode
        public float stretchLimit;  // pro AddJoint
        public float compressLimit; // pro AddJoint
        public float momentLimit;   // pro AddJoint

        public readonly (int, int) EdgePairId => indexA < indexB ? (indexA, indexB) : (indexB, indexA);
        
        public void ClearAddJoint() => Command = Command switch 
        {
            SpCommand.AddJoint => SpCommand.None,
            SpCommand.AddNodeAndJoint => SpCommand.AddNode,
            _ => Command
        };
    }

    // pro zadavani sil s platnosti po dobu jen jednoho Framu
    public struct ForceCommand
    {
        public int indexA;
        public Vector2 forceA;
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