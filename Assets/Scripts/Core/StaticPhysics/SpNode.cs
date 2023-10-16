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
        public int isFixedRoot;
        public Placeable placeable;
        public EdgeEnd[] newEdges;
        public int newEdgeCount;

        internal readonly bool ConnectsTo(int index)
        {
            for (int f = 0; f < edges.Length; f++)
            {
                if (edges[f].Other == index)
                    return true;
            }
            return false;
        }

        internal readonly bool ConnectsTo(int index, out int joint)
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

        internal readonly ref EdgeEnd GetEnd(int from) => ref GetEnd(from, edges);
        internal readonly ref EdgeEnd GetEndNew(int from) => ref GetEnd(from, newEdges);

        internal static ref EdgeEnd GetEnd(int from, EdgeEnd[] edges)
        {
            for (int f = 0; f < edges.Length; f++)
            {
                if (edges[f].Other == from)
                {
                    return ref edges[f];
                }
            }
            throw new InvalidOperationException("Hranda tu neni");
        }

        public readonly float ShortestColorDistance(int color) => ShortestColorDistance(color, edges, isFixedRoot);
        public readonly float ShortestColorDistanceNew(int color) => ShortestColorDistance(color, newEdges, isFixedRoot);

        private static float ShortestColorDistance(int color, EdgeEnd[] edges, int isFixedRoot)
        {
            if (isFixedRoot == color)
                return 0;
            float ret = float.MaxValue;
            for (int f = 0; f < edges.Length; f++)
            {
                if (edges[f].Out0Root == color && edges[f].Out0Lengh < ret)
                    ret = edges[f].Out0Lengh;
                if (edges[f].Out1Root == color && edges[f].Out1Lengh < ret)
                    ret = edges[f].Out1Lengh;
            }
            return ret;
        }

        public readonly float BestDistance(out int color) => BestDistance(out color, edges, isFixedRoot);
        public readonly float BestDistanceNew(out int color) => BestDistance(out color, newEdges, isFixedRoot);

        private static float BestDistance(out int color, EdgeEnd[] edges, int isFixedRoot)
        {
            color = -1;
            if (isFixedRoot != 0)
            {
                color = isFixedRoot;
                return 0;
            }
                
            float ret = float.MaxValue;
            for (int f = 0; f < edges.Length; f++)
            {
                if (edges[f].Out0Root != 0 && edges[f].Out0Lengh < ret)
                {
                    color = edges[f].Out0Root;
                    ret = edges[f].Out0Lengh;
                }
                if (edges[f].Out1Root != 0 && edges[f].Out1Lengh < ret)
                {
                    color = edges[f].Out1Root;
                    ret = edges[f].Out1Lengh;
                }
            }
            return ret;
        }


        public readonly bool FindOtherColor(int color1, out int color2, out float length2, bool useNewEges) => FindOtherColor(color1, useNewEges ? newEdges : edges, out color2, out length2);

        private static bool FindOtherColor(int color1, EdgeEnd[] edges, out int color2, out float length2)
        {
            color2 = 0;
            length2 = float.MaxValue;

            for (int f = 0; f < edges.Length; f++)
            {
                if (edges[f].Out0Root != color1 && edges[f].Out1Root != color1)
                {
                    if (edges[f].Out0Root != 0 && edges[f].Out0Lengh < length2)
                    {
                        color2 = edges[f].Out0Root;
                        length2 = edges[f].Out0Lengh;
                    }
                }
            }
            return color2 != 0;
        }

        public readonly float GetInvLenSum(int color, bool useNewEges) => GetInvLenSum(color, useNewEges ? newEdges : edges);

        private static float GetInvLenSum(int color, EdgeEnd[] edges)
        {
            float sum = 0;

            for (int f = 0; f < edges.Length; f++)
            {
                if (edges[f].Out0Root == color)
                    sum += 1 / edges[f].Out0Lengh;
                if (edges[f].Out1Root == color)
                    sum += 1 / edges[f].Out1Lengh;
            }
            return sum;
        }

        //public readonly ref EdgeEnd DistanceAndEdgeEnd(int from, int color, out float distance) => ref DistanceAndEdgeEnd(from, color, edges, isFixedRoot, out distance);
        //public readonly ref EdgeEnd DistanceAndEdgeEndNew(int from, int color, out float distance) => ref DistanceAndEdgeEnd(from, color, newEdges, isFixedRoot, out distance);

        //private static ref EdgeEnd DistanceAndEdgeEnd(int from, int color, EdgeEnd[] edges, int isFixedRoot, out float distance)
        //{
        //    distance = isFixedRoot == color ? 0 : float.MaxValue;
        //    ref EdgeEnd end = ref edges[0];
        //    for (int f = 0; f < edges.Length; f++)
        //    {
        //        if (edges[f].Out0Root == color && edges[f].Out0Lengh < distance)
        //            distance = edges[f].Out0Lengh;
        //        if (edges[f].Out1Root == color && edges[f].Out1Lengh < distance)
        //            distance = edges[f].Out1Lengh;
        //        if (edges[f].Other == from)
        //            end = ref edges[f];
        //    }
        //    return ref end;
        //}
    }
}
