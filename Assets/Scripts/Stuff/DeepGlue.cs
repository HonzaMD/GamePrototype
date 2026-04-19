using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Stuff
{
    [RequireComponent(typeof(PlaceableSibling))]
    public class DeepGlue : MonoBehaviour, IHasCleanup, ICanActivate, IPhysicsEvents
    {
        private const float SearchRadius = 1.5f;
        private const float TouchMargin = 0.05f;
        private const float StrengthenFactor = 2f;

        private bool isActive;

        private static readonly SpanList<EdgePlan> plannedEdges = new();
        private static readonly List<PairCandidate> plannedEdgesCandidates = new();
        private static readonly List<NeighborStrength> tmpNeighbors = new();
        private static readonly List<int> propWorklist = new();

        private enum EdgeKind : byte { New, ExistingRb, ExistingSp }

        private struct EdgePlan
        {
            public Placeable a;
            public Placeable b;
            public RbJoint joint;
            public EdgeKind kind;
            public bool markSp;
            public bool pruned;
            public float stretchLimit;
            public float compressLimit;
            public float momentLimit;
            public float strength;
            // Adjacency list pointers (1-based index do plannedEdges, 0 = konec).
            // nextA = dalsi hrana incidentni k uzlu 'a', nextB = dalsi hrana incidentni k 'b'.
            // Pri prochazeni z uzlu n vezmes nextA pokud f.a == n, jinak nextB.
            public int nextA;
            public int nextB;
        }

        private struct PairCandidate
        {
            public Placeable src;
            public Placeable dst;
        }

        private struct NeighborStrength
        {
            public Placeable node;
            public float strength;
        }

        public void Activate() => isActive = true;

        void IPhysicsEvents.OnCollisionEnter(Collision collision, Label otherLabel)
        {
            if (!isActive)
                return;
            isActive = false;

            var myPlaceable = GetComponent<Placeable>();
            var map = myPlaceable.GetMap();

            DoGlue(myPlaceable, map);
            SpawnEffect(myPlaceable);
            myPlaceable.Kill();
        }

        void IPhysicsEvents.OnCollisionStay(Collision collision, Label otherLabel) { }

        public void Cleanup(bool goesToInventory) => isActive = false;

        private static void SpawnEffect(Placeable myPlaceable)
        {
            var pe = Game.Instance.PrefabsStore.ParticleEffect.CreateWithotInit(myPlaceable.LevelGroup, myPlaceable.Center3D);
            pe.Init(10);
        }

        private static void DoGlue(Placeable myPlaceable, Map.Map map)
        {
            plannedEdges.Clear();
            plannedEdgesCandidates.Clear();

            var nodesBuf = ListPool<Placeable>.Rent();
            var touchBuf = ListPool<Placeable>.Rent();
            try
            {
                FindNodesInRadius(myPlaceable, map, nodesBuf);
                CollectExistingEdges(map, nodesBuf);
                CollectNewCandidates(map, nodesBuf, touchBuf);
                FinalDedupAndAddNew(map, nodesBuf);
                BuildAdjacency();
                try
                {
                    TrianglePrune();
                    PropagateSp();
                }
                finally
                {
                    AdejacencyCleanup();
                }
                ApplyChanges();
            }
            finally
            {
                nodesBuf.Return();
                touchBuf.Return();
                plannedEdgesCandidates.Clear();
                plannedEdges.Clear();
                tmpNeighbors.Clear();
                propWorklist.Clear();
            }
        }

        private static void FindNodesInRadius(Placeable myPlaceable, Map.Map map, List<Placeable> nodesBuf)
        {
            Vector2 r = new Vector2(SearchRadius, SearchRadius);
            Vector2 size = new Vector2(SearchRadius * 2f, SearchRadius * 2f);
            int tag = map.GetNextTag();
            myPlaceable.Tag = tag;
            map.Get(nodesBuf, myPlaceable.Center - r, size, Ksid.SpNode, tag);
        }

        private static void CollectExistingEdges(Map.Map map, List<Placeable> nodesBuf)
        {
            int dedupTag = map.GetNextTag();
            for (int ni = 0; ni < nodesBuf.Count; ni++)
            {
                var n = nodesBuf[ni];
                var pt = n.ParentForConnections;
                int childCount = pt.childCount;
                for (int ci = 0; ci < childCount; ci++)
                {
                    if (!pt.GetChild(ci).TryGetComponent(out RbJoint j))
                        continue;
                    if (!j.IsConnected)
                        continue;
                    var other = j.OtherObj;
                    if (other.Tag == dedupTag)
                        continue;
                    if (!other.Ksid.IsChildOfOrEq(Ksid.SpNode))
                        continue;

                    EdgeKind kind = (j.state & RbJoint.State.SpConnection) != 0
                        ? EdgeKind.ExistingSp
                        : EdgeKind.ExistingRb;
                    AddEdge(n, other, kind, j);
                }
                n.Tag = dedupTag;
            }
        }

        private static void CollectNewCandidates(Map.Map map, List<Placeable> nodesBuf, List<Placeable> touchBuf)
        {
            for (int ni = 0; ni < nodesBuf.Count; ni++)
            {
                var n = nodesBuf[ni];
                bool nFixed = n.Ksid.IsChildOfOrEq(Ksid.SpFixed);
                touchBuf.Clear();
                n.FindTouchingObjs(map, touchBuf, Ksid.SpNode, TouchMargin);
                for (int ti = 0; ti < touchBuf.Count; ti++)
                {
                    var c = touchBuf[ti];
                    if (nFixed && c.Ksid.IsChildOfOrEq(Ksid.SpFixed))
                        continue;
                    if (n.TryFindRbJoint(c, out _))
                        continue;
                    plannedEdgesCandidates.Add(new PairCandidate { src = n, dst = c });
                }
            }
        }

        private static void FinalDedupAndAddNew(Map.Map map, List<Placeable> nodesBuf)
        {
            int dedupTag = map.GetNextTag();
            for (int ni = 0; ni < nodesBuf.Count; ni++)
                nodesBuf[ni].Tag = dedupTag;

            Placeable currentSrc = null;
            int candCount = plannedEdgesCandidates.Count;
            for (int ci = 0; ci < candCount; ci++)
            {
                var pair = plannedEdgesCandidates[ci];
                if (pair.src != currentSrc)
                {
                    if (currentSrc != null)
                        currentSrc.Tag = 0;
                    currentSrc = pair.src;
                }
                if (pair.src.Tag == dedupTag && pair.dst.Tag == dedupTag)
                    AddEdge(pair.src, pair.dst, EdgeKind.New, null);
            }
        }

        private static void AddEdge(Placeable a, Placeable b, EdgeKind kind, RbJoint joint)
        {
            var la = a.SpLimits;
            var lb = b.SpLimits;
            float s = Mathf.Min(la.StretchLimit, lb.StretchLimit);
            float c = Mathf.Min(la.CompressLimit, lb.CompressLimit);
            float m = Mathf.Min(la.MomentLimit, lb.MomentLimit);
            plannedEdges.Add(new EdgePlan
            {
                a = a,
                b = b,
                kind = kind,
                joint = joint,
                stretchLimit = s,
                compressLimit = c,
                momentLimit = m,
                strength = Mathf.Min(Mathf.Min(s, c), m),
            });
        }

        // Adjacency list: per-node head je v Placeable.Tag (1-based index do plannedEdges, 0 = zadne hrany).
        // Pro hranu i nactenou pres jeji uzel n: dalsi hrana incidentni k n je e.nextA pokud e.a == n, jinak e.nextB.
        // Build vlozi i+1 do Tag obou endpointu a stary Tag (= predchozi head, nebo 0) ulozi do prislusneho next pointeru.
        // Tag MUSI byt na konci vynulovan (AdejacencyCleanup) — viz feedback memory o Placeable.Tag.
        private static void BuildAdjacency()
        {
            AdejacencyCleanup();

            int count = plannedEdges.Count;
            for (int i = 0; i < count; i++)
            {
                ref var e = ref plannedEdges[i];
                e.nextA = e.a.Tag;
                e.nextB = e.b.Tag;
                e.a.Tag = i + 1;
                e.b.Tag = i + 1;
            }
        }

        private static void AdejacencyCleanup()
        {
            int count = plannedEdges.Count;
            for (int i = 0; i < count; i++)
            {
                ref var e = ref plannedEdges[i];
                e.a.Tag = 0;
                e.b.Tag = 0;
            }
        }

        private static void TrianglePrune()
        {
            int count = plannedEdges.Count;

            for (int ei = 0; ei < count; ei++)
            {
                ref var e = ref plannedEdges[ei];
                if (e.kind != EdgeKind.New || e.pruned)
                    continue;

                var a = e.a;
                var b = e.b;
                tmpNeighbors.Clear();

                int idx = a.Tag;
                while (idx != 0)
                {
                    int fi = idx - 1;
                    ref var f = ref plannedEdges[fi];
                    Placeable x;
                    int next;
                    if (f.a == a) { x = f.b; next = f.nextA; }
                    else { x = f.a; next = f.nextB; }

                    if (fi != ei && !f.pruned && x != b)
                        tmpNeighbors.Add(new NeighborStrength { node = x, strength = f.strength });

                    idx = next;
                }

                idx = b.Tag;
                while (idx != 0)
                {
                    int gi = idx - 1;
                    ref var g = ref plannedEdges[gi];
                    Placeable x;
                    int next;
                    if (g.a == b) { x = g.b; next = g.nextA; }
                    else { x = g.a; next = g.nextB; }

                    if (gi != ei && !g.pruned && x != a)
                    {
                        float fStrength = 0f;
                        bool found = false;
                        for (int k = 0; k < tmpNeighbors.Count; k++)
                        {
                            if (tmpNeighbors[k].node == x)
                            {
                                fStrength = tmpNeighbors[k].strength;
                                found = true;
                                break;
                            }
                        }

                        if (found && e.strength <= fStrength && e.strength <= g.strength)
                        {
                            e.pruned = true;
                            break;
                        }
                    }

                    idx = next;
                }
            }
        }


        private static void PropagateSp()
        {
            int count = plannedEdges.Count;
            propWorklist.Clear();

            for (int i = 0; i < count; i++)
            {
                ref var e = ref plannedEdges[i];
                if (e.pruned) continue;
                bool initial = e.kind == EdgeKind.ExistingSp
                    || e.a.Ksid.IsChildOfOrEq(Ksid.SpFixed)
                    || e.b.Ksid.IsChildOfOrEq(Ksid.SpFixed)
                    || e.a.SpNodeIndex != 0
                    || e.b.SpNodeIndex != 0;
                if (initial)
                {
                    e.markSp = true;
                    propWorklist.Add(i);
                }
            }

            int wi = 0;
            while (wi < propWorklist.Count)
            {
                int ei = propWorklist[wi++];
                ref var e = ref plannedEdges[ei];
                SpreadSp(e.a, ei);
                SpreadSp(e.b, ei);
            }
        }

        private static void SpreadSp(Placeable n, int sourceEi)
        {
            int idx = n.Tag;
            while (idx != 0)
            {
                int fi = idx - 1;
                ref var f = ref plannedEdges[fi];
                int next = (f.a == n) ? f.nextA : f.nextB;

                if (fi != sourceEi && !f.pruned && !f.markSp)
                {
                    f.markSp = true;
                    propWorklist.Add(fi);
                }

                idx = next;
            }
        }

        private static void ApplyChanges()
        {
            int count = plannedEdges.Count;
            for (int i = 0; i < count; i++)
            {
                ref var e = ref plannedEdges[i];
                if (e.pruned) continue;

                switch (e.kind)
                {
                    case EdgeKind.New:
                        var newJoint = e.a.CreateRbJoint(e.b);
                        if (e.markSp)
                            newJoint.SetupSp();
                        else
                            newJoint.SetupRb();
                        break;

                    case EdgeKind.ExistingRb:
                        if (e.markSp)
                            e.joint.SetupSp();
                        break;

                    case EdgeKind.ExistingSp:
                        {
                            int idxA = e.joint.MyObj.SpNodeIndex;
                            int idxB = e.joint.OtherObj.SpNodeIndex;
                            Game.Instance.StaticPhysics.UpdateJointLimits(
                                idxA, idxB,
                                e.stretchLimit * StrengthenFactor,
                                e.compressLimit * StrengthenFactor,
                                e.momentLimit * StrengthenFactor);
                        }
                        break;
                }
            }
        }
    }
}
