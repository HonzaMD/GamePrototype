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
        internal readonly ref EdgeEnd GetEndAny(int from) => ref GetEnd(from, newEdges ?? edges);

        internal static ref EdgeEnd GetEnd(int from, EdgeEnd[] edges)
        {
            for (int f = 0; f < edges.Length; f++)
            {
                if (edges[f].Other == from)
                {
                    return ref edges[f];
                }
            }
            throw new InvalidOperationException("Hrana tu neni");
        }

        public readonly float ShortestColorDistance(int color) => ShortestColorDistance(color, edges, isFixedRoot);
        public readonly float ShortestColorDistanceNew(int color) => ShortestColorDistance(color, newEdges, isFixedRoot);
        public readonly float ShortestColorDistanceAny(int color) => ShortestColorDistance(color, newEdges ?? edges, isFixedRoot);

        private static float ShortestColorDistance(int color, EdgeEnd[] edges, int isFixedRoot)
        {
            if (isFixedRoot == color)
                return 0;
            float ret = float.MaxValue;
            for (int f = 0; f < edges.Length; f++)
            {
                if (edges[f].Out0Root == color && edges[f].Out0Length < ret)
                    ret = edges[f].Out0Length;
                if (edges[f].Out1Root == color && edges[f].Out1Length < ret)
                    ret = edges[f].Out1Length;
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
                if (edges[f].Out0Root != 0 && edges[f].Out0Length < ret)
                {
                    color = edges[f].Out0Root;
                    ret = edges[f].Out0Length;
                }
                if (edges[f].Out1Root != 0 && edges[f].Out1Length < ret)
                {
                    color = edges[f].Out1Root;
                    ret = edges[f].Out1Length;
                }
            }
            return ret;
        }

        public readonly bool IsConnectedToRoot()
        {
            if (isFixedRoot != 0)
                return true;

            for (int f = 0; f < edges.Length; f++)
            {
                if (edges[f].Out0Root != 0)
                    return true;
            }

            return false;
        }


        public readonly bool FindOtherColor(int color1, out int color2, out float length2, out float strength1, out float strength2)
            => FindOtherColor(color1, edges, out color2, out length2, out strength1, out strength2);

        private static bool FindOtherColor(int color1, EdgeEnd[] edges, out int color2, out float length2, out float strength1, out float strength2)
        {
            color2 = 0;
            length2 = float.MaxValue;

            for (int f = 0; f < edges.Length; f++)
            {
                if (edges[f].Out0Root != color1 && edges[f].Out1Root != color1)
                {
                    if (edges[f].Out0Root != 0 && edges[f].Out0Length < length2)
                    {
                        color2 = edges[f].Out0Root;
                        length2 = edges[f].Out0Length;
                    }
                }
            }

            strength1 = 0;
            strength2 = 0;
            if (color2 != 0)
            {
                for (int f = 0; f < edges.Length; f++)
                {
                    if (edges[f].Out0Root == color1 && edges[f].Out0Strength > strength1)
                        strength1 = edges[f].Out0Strength;
                    if (edges[f].Out1Root == color1 && edges[f].Out1Strength > strength1)
                        strength1 = edges[f].Out1Strength;

                    if (edges[f].Out0Root == color2 && edges[f].Out0Strength > strength2)
                        strength2 = edges[f].Out0Strength;
                    if (edges[f].Out1Root == color2 && edges[f].Out1Strength > strength2)
                        strength2 = edges[f].Out1Strength;
                }
            }
            return color2 != 0;
        }


        public float FindOutStrengthNew(int color) => FindOutStrength(color, newEdges, isFixedRoot);
        public float FindOutStrengthAny(int color) => FindOutStrength(color, newEdges ?? edges, isFixedRoot);

        private static float FindOutStrength(int color, EdgeEnd[] edges, int isFixedRoot)
        {
            if (isFixedRoot == color)
                return float.PositiveInfinity; // root unese cokoliv, pevnost prvni hrany pak urcuje jen MinLimit jointu
            float strength = 0;
            for (int f = 0; f < edges.Length; f++)
            {
                if (edges[f].Out0Root == color && edges[f].Out0Strength > strength)
                    strength = edges[f].Out0Strength;
                if (edges[f].Out1Root == color && edges[f].Out1Strength > strength)
                    strength = edges[f].Out1Strength;
            }
            return strength;
        }

        // Spocita vsechny sumy potrebne pro vazene rozdeleni sily mezi vystupnimi cestami barvy.
        // Sumy mohou byt 0 nebo Infinity; SafeWeight pri nasledne aplikaci dela fallback na 1/count.
        public readonly WeightSums GetCombinedSums(int color)
            => GetCombinedSums(color, edges);

        private static WeightSums GetCombinedSums(int color, EdgeEnd[] edges)
        {
            float invLenSum = 0;
            float strSum = 0;
            int count = 0;

            for (int f = 0; f < edges.Length; f++)
            {
                if (edges[f].Out0Root == color)
                {
                    invLenSum += 1f / edges[f].Out0Length;
                    strSum += edges[f].Out0Strength;
                    count++;
                }
                if (edges[f].Out1Root == color)
                {
                    invLenSum += 1f / edges[f].Out1Length;
                    strSum += edges[f].Out1Strength;
                    count++;
                }
            }

            float pSum = 0;
            for (int f = 0; f < edges.Length; f++)
            {
                if (edges[f].Out0Root == color)
                    pSum += ForceSplitWeighting.CombinedP(1f / edges[f].Out0Length, edges[f].Out0Strength, invLenSum, strSum, count);
                if (edges[f].Out1Root == color)
                    pSum += ForceSplitWeighting.CombinedP(1f / edges[f].Out1Length, edges[f].Out1Strength, invLenSum, strSum, count);
            }

            return new WeightSums(invLenSum, strSum, pSum, count);
        }

        internal void EnsureNewEdges(SpDataManager data)
        {
            if (newEdges == null)
            {
                newEdges = data.GetEdgeArr(edges.Length);

                for (int f = 0; f < edges.Length; f++)
                {
                    newEdges[f] = edges[f];
                }
                newEdgeCount = edges.Length;
            }
        }

        //public readonly ref EdgeEnd DistanceAndEdgeEnd(int from, int color, out float distance) => ref DistanceAndEdgeEnd(from, color, edges, isFixedRoot, out distance);
        //public readonly ref EdgeEnd DistanceAndEdgeEndNew(int from, int color, out float distance) => ref DistanceAndEdgeEnd(from, color, newEdges, isFixedRoot, out distance);

        //private static ref EdgeEnd DistanceAndEdgeEnd(int from, int color, EdgeEnd[] edges, int isFixedRoot, out float distance)
        //{
        //    distance = isFixedRoot == color ? 0 : float.MaxValue;
        //    ref EdgeEnd end = ref edges[0];
        //    for (int f = 0; f < edges.Length; f++)
        //    {
        //        if (edges[f].Out0Root == color && edges[f].Out0Length < distance)
        //            distance = edges[f].Out0Length;
        //        if (edges[f].Out1Root == color && edges[f].Out1Length < distance)
        //            distance = edges[f].Out1Length;
        //        if (edges[f].Other == from)
        //            end = ref edges[f];
        //    }
        //    return ref end;
        //}
    }
}
