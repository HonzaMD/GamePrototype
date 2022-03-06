using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Core.StaticPhysics
{
    struct SpNode
    {
        public Vector2 position;
        public EdgeEnd[] edges;
        public Vector2 force;
        public bool isFixed;
        public Placeable placeable;
        public EdgeEnd[] newEdges;
        public int newEdgeCount;

        internal bool ConnectsTo(int index)
        {
            for (int f = 0; f < edges.Length; f++)
            {
                if (edges[f].Other == index)
                    return true;
            }
            return false;
        }

        internal bool ConnectsTo(int index, out int joint)
        {
            for (int f = 0; f < edges.Length; f++)
            {
                if (edges[f].Other == index)
                {
                    joint = edges[f].Joint;
                    return true;
                }
            }
            joint = -1;
            return false;
        }
    }
}
