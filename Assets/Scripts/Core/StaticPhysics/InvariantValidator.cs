using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Assertions;

namespace Assets.Scripts.Core.StaticPhysics
{
    // Po dokonceni ApplyChanges projde vsechny zive uzly a overi globalni invarianty grafu.
    // Cele Validate je [Conditional("UNITY_ASSERTIONS")] - v release/non-dev buildu nulovy overhead.
    internal class InvariantValidator
    {
        private const float Eps = 1e-3f;

        private readonly SpDataManager data;
        private bool befereRemoveForce;

        public InvariantValidator(SpDataManager data)
        {
            this.data = data;
        }

        [Conditional("UNITY_ASSERTIONS")]
        [Conditional("DEBUG_SP")]
        public void Validate(bool befereRemoveForce, HashSet<int> deletedNodes = null)
        {
            this.befereRemoveForce = befereRemoveForce;
            int top = data.NodesTopIndex;
            for (int i = 1; i <= top; i++)
            {
                if (!data.IsNodeValid(i) || deletedNodes?.Contains(i) == true)
                    continue;
                ValidateNode(i);
            }
        }

        private void ValidateNode(int i)
        {
            ref var node = ref data.GetNode(i);

            if (!befereRemoveForce)
            {
                Assert.IsNull(node.newEdges, $"Node {i}: newEdges should be null after ApplyChanges");
                Assert.AreEqual(0, node.newEdgeCount, $"Node {i}: newEdgeCount should be 0");
            }

            var edges = node.edges;

            // Self-edge a duplicitni Other zakazane
            for (int f = 0; f < edges.Length; f++)
            {
                Assert.AreNotEqual(i, edges[f].Other, $"Node {i}: self-edge");
                for (int g = f + 1; g < edges.Length; g++)
                    Assert.AreNotEqual(edges[f].Other, edges[g].Other, $"Node {i}: duplicitni hrana do {edges[f].Other}");
            }

            // Zivy uzel musi byt napojeny na koren (jinak ho mel FindFallenWorker odstranit)
            if (!befereRemoveForce)
                Assert.IsTrue(node.IsConnectedToRoot(), $"Node {i}: zivy uzel neni napojeny na koren");

            // FixedRoot: SCD vlastni barvy = 0, zadna Out hrana vlastni barvy
            if (node.isFixedRoot != 0)
            {
                Assert.AreEqual(0f, node.ShortestColorDistance(node.isFixedRoot),
                    $"Node {i}: fixed root ma nenulove SCD vlastni barvy");
                for (int f = 0; f < edges.Length; f++)
                {
                    Assert.AreNotEqual(node.isFixedRoot, edges[f].Out0Root,
                        $"Node {i}: fixed root ma Out0 vlastni barvy");
                    Assert.AreNotEqual(node.isFixedRoot, edges[f].Out1Root,
                        $"Node {i}: fixed root ma Out1 vlastni barvy");
                }
            }

            for (int f = 0; f < edges.Length; f++)
            {
                ValidateEdge(i, ref node, ref edges[f]);
            }
        }

        private void ValidateEdge(int i, ref SpNode node, ref EdgeEnd edge)
        {
            int otherIdx = edge.Other;
            Assert.IsTrue(data.IsNodeValid(otherIdx), $"Edge {i}->{otherIdx}: druhy konec neexistuje");
            ref var other = ref data.GetNode(otherIdx);

            // Mirror: druhy konec hrany musi sedet
            ref var mirror = ref other.GetEnd(i);
            Assert.AreEqual(edge.Joint, mirror.Joint, $"Edge {i}-{otherIdx}: Joint mismatch");
            Assert.AreEqual(edge.Out0Root, mirror.In0Root, $"Edge {i}->{otherIdx}: Out0Root != mirror.In0Root");
            Assert.AreEqual(edge.Out1Root, mirror.In1Root, $"Edge {i}->{otherIdx}: Out1Root != mirror.In1Root");
            Assert.AreEqual(edge.In0Root, mirror.Out0Root, $"Edge {i}<-{otherIdx}: In0Root != mirror.Out0Root");
            Assert.AreEqual(edge.In1Root, mirror.Out1Root, $"Edge {i}<-{otherIdx}: In1Root != mirror.Out1Root");

            // Stejna barva nemuze byt v obou Out slotech
            if (edge.Out0Root != 0)
                Assert.AreNotEqual(edge.Out0Root, edge.Out1Root,
                    $"Edge {i}->{otherIdx}: stejna barva v Out0 i Out1");
            // a take ne v obou In slotech
            if (edge.In0Root != 0)
                Assert.AreNotEqual(edge.In0Root, edge.In1Root,
                    $"Edge {i}->{otherIdx}: stejna barva v In0 i In1");

            // Slot 0 musi byt vzdy lepsi nez slot 1 (kdyz oba obsazene)
            if (edge.Out0Root != 0 && edge.Out1Root != 0)
                Assert.IsTrue(Utils.IsDistanceBetter(edge.Out0Length, edge.Out1Length, edge.Out0Root, edge.Out1Root),
                    $"Edge {i}->{otherIdx}: Out0 (len={edge.Out0Length}, root={edge.Out0Root}) neni lepsi nez Out1 (len={edge.Out1Length}, root={edge.Out1Root})");

            ref var joint = ref data.GetJoint(edge.Joint);

            if (edge.Out0Root != 0)
                ValidateOutSlot(i, otherIdx, edge.Out0Root, edge.Out0Length, edge.Out0Strength, ref node, ref other, ref joint);
            if (edge.Out1Root != 0)
                ValidateOutSlot(i, otherIdx, edge.Out1Root, edge.Out1Length, edge.Out1Strength, ref node, ref other, ref joint);
        }

        private void ValidateOutSlot(int from, int to, int color, float length, float strength,
            ref SpNode node, ref SpNode other, ref SpJoint joint)
        {
            // Length = other.SCD(color) + joint.length
            float otherSCD = other.ShortestColorDistance(color);
            Assert.AreApproximatelyEqual(otherSCD + joint.length, length, Eps,
                $"Edge {from}->{to} color {color}: length mismatch (other.SCD={otherSCD}, joint.len={joint.length}, edge.len={length})");

            // SCD ordering: Out hrana vede do uzlu s nizsim SCD; rovnost dovolena jen pro nulovou hranu
            float fromSCD = node.ShortestColorDistance(color);
            Assert.IsTrue(fromSCD <= length + Eps,
                $"Edge {from}->{to} color {color}: fromSCD ({fromSCD}) > edge.length ({length})");
            if (joint.length > Eps)
                Assert.IsTrue(otherSCD < fromSCD - Eps * 0.5f,
                    $"Edge {from}->{to} color {color}: ordering broken (otherSCD={otherSCD} >= fromSCD={fromSCD}, joint.len={joint.length})");
            else
                Assert.IsTrue(otherSCD <= fromSCD + Eps,
                    $"Edge {from}->{to} color {color}: zero-length edge a otherSCD ({otherSCD}) > fromSCD ({fromSCD})");

            if (!befereRemoveForce)
            {
                // Strength = Min(joint.MinLimit, other.MaxOutStrength(color))
                float expected = Mathf.Min(joint.MinLimit, other.FindOutStrength(color));
                Assert.AreApproximatelyEqual(expected, strength, Eps,
                    $"Edge {from}->{to} color {color}: strength mismatch (expected {expected}, got {strength})");
            }
        }
    }
}
