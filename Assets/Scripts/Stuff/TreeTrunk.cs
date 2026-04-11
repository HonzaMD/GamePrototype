using Assets.Scripts.Bases;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Stuff
{
    public class TreeTrunk : Placeable, ISimpleTimerConsumer
    {
        // Stromova struktura
        [HideInInspector]
        public TreeTrunk Parent;
        [HideInInspector]
        public TreeTrunk FirstChild;
        [HideInInspector]
        public TreeTrunk NextSibling;
        [HideInInspector]
        public TreeTrunk Controller;
        [HideInInspector]
        public Dir8 Direction;

        // Konfigurace (nastaveno na prefabu nebo pri spawnu)
        public TreeSettings TreeSettings;

        // Pocet segmentu: 1 (ja) + pocet vsech potomku
        public int SegmentCount { get; private set; }
        private bool isGrowing;
        private bool hasBranch;

        // ISimpleTimerConsumer
        private int timerTag;
        int ISimpleTimerConsumer.ActiveTag { get => timerTag; set => timerTag = value; }

        private bool IsController => Controller == this;

        private struct GrowthCandidate
        {
            public TreeTrunk Source;
            public Dir8 Direction;
            public float Weight;
        }

        private static readonly List<GrowthCandidate> candidates = new List<GrowthCandidate>();

        protected override void AfterMapPlaced(Map.Map map, bool goesFromInventory)
        {
            if (Parent == null && Controller == null)
            {
                // Jsme ridici uzel
                Controller = this;
                SegmentCount = 1;

                var segments = ListPool<Placeable>.Rent();
                segments.Add(this);
                Physics.SyncTransforms();
                var connector = new SpConnectionFinder(segments, map);
                connector.TryConnect();
                segments.Return();

                StartGrowth();
            }
        }

        public override void Cleanup(bool goesToInventory)
        {
            StopGrowth();
            RemoveFromParent();
            DetachChildren();
            ReturnBranchesToPool();

            Parent = null;
            FirstChild = null;
            NextSibling = null;
            Controller = null;
            SegmentCount = 0;
            isGrowing = false;

            base.Cleanup(goesToInventory);
        }

        // Rust

        public void StartGrowth()
        {
            if (IsController && TreeSettings != null && !isGrowing)
            {
                isGrowing = true;
                this.Plan(TreeSettings.GetGrowthDelay());
            }
        }

        public void StopGrowth()
        {
            isGrowing = false;
            timerTag++;
        }

        void ISimpleTimerConsumer.OnTimer()
        {
            if (HasActiveRB)
            {
                StopGrowth();
                return;
            }

            if (SegmentCount >= TreeSettings.MaxSegments)
                return;

            TryGrow();

            if (isGrowing)
                this.Plan(TreeSettings.GetGrowthDelay());
        }

        private void TryGrow()
        {
            var map = GetMap();
            candidates.Clear();
            float totalWeight = 0f;

            CollectCandidates(this, map, ref totalWeight);

            if (candidates.Count > 0 && totalWeight > 0f)
            {
                // Vazeny nahodny vyber
                float roll = Random.Range(0f, totalWeight);
                float cumulative = 0f;
                for (int i = 0; i < candidates.Count; i++)
                {
                    cumulative += candidates[i].Weight;
                    if (roll <= cumulative)
                    {
                        var c = candidates[i];
                        GrowSegment(c.Source, c.Direction, c.Source.Pivot + c.Direction.ToVector(), map);
                        break;
                    }
                }
            }

            candidates.Clear();
        }

        private void CollectCandidates(TreeTrunk node, Map.Map map, ref float totalWeight)
        {
            int cellZ = node.CellZ;

            // Odbocky - vsechny smery krome opacneho (a opacny jen pokud nema parenta)
            for (int d = 0; d < 8; d++)
            {
                var dir = (Dir8)d;
                if (node.Parent != null && dir == node.Direction.Opposite())
                    continue;

                bool direct = dir == node.Direction || dir == node.Direction.Opposite();
                float weight = direct ? 1f : TreeSettings.BranchWeight;
                if (!direct)
                {
                    if (HasNearbyBranch(node))
                        weight *= TreeSettings.NearBranchWeight;
                    if (node.FirstChild == null)
                        weight *= TreeSettings.EndBranchPenalty;
                }

                if (weight <= 0)
                    continue;

                TryAddCandidate(node, dir, map, cellZ, weight, ref totalWeight);
            }

            // Rekurze do deti
            var child = node.FirstChild;
            while (child != null)
            {
                CollectCandidates(child, map, ref totalWeight);
                child = child.NextSibling;
            }
        }

        private void TryAddCandidate(TreeTrunk source, Dir8 dir, Map.Map map, int cellZ, float weight, ref float totalWeight)
        {
            var targetPos = source.Pivot + dir.ToVector();
            if (!IsCellFree(map, targetPos, cellZ))
                return;

            weight *= dir.UpWeight(TreeSettings.UpExponent);
            weight *= GetFreeNeighborWeight(map, targetPos, cellZ);
            weight *= GetCenterWeight(targetPos.x);

            if (weight <= 0)
                return;

            candidates.Add(new GrowthCandidate
            {
                Source = source,
                Direction = dir,
                Weight = weight,
            });
            totalWeight += weight;
        }

        private static bool HasNearbyBranch(TreeTrunk node)
        {
            // Kontrola: parent, nebo nektery syn ma branch
            if (node.Parent != null && node.Parent.hasBranch)
                return true;
            var child = node.FirstChild;
            while (child != null)
            {
                if (child.hasBranch)
                    return true;
                child = child.NextSibling;
            }
            return false;
        }

        private float GetCenterWeight(float targetX)
        {
            float dist = Mathf.Abs(targetX - Pivot.x);
            return 1f / (1f + dist * TreeSettings.CenterPreference);
        }

        private bool IsCellFree(Map.Map map, Vector2 targetPos, int cellZ)
        {
            var blocking = map.GetCellBlocking(targetPos);
            return !blocking.HasSubFlag(SubCellFlags.Part, cellZ);
        }

        private float GetFreeNeighborWeight(Map.Map map, Vector2 targetPos, int cellZ)
        {
            int freeCount = 0;
            for (int d = 0; d < 8; d++)
            {
                var neighborPos = targetPos + ((Dir8)d).ToVector();
                var blocking = map.GetCellBlocking(neighborPos);
                if (!blocking.HasSubFlag(SubCellFlags.Part, cellZ))
                    freeCount++;
            }

            var weight = freeCount / 8f;
            return weight * weight;
        }

        private void GrowSegment(TreeTrunk parentTrunk, Dir8 direction, Vector2 targetPos, Map.Map map)
        {
            var pos = new Vector3(targetPos.x, targetPos.y, parentTrunk.transform.position.z);
            bool isDiagonal = direction.IsDiagonal();

            // Vyber spravny prefab
            var prefab = isDiagonal
                ? Game.Instance.PrefabsStore.TreeTrunkDg
                : Game.Instance.PrefabsStore.TreeTrunk;

            var newTrunk = prefab.CreateWithotInit(LevelGroup, pos);

            // Nastav rotaci
            newTrunk.transform.rotation = direction.ToRotation();

            // Nastav stromovou strukturu pred Init
            newTrunk.Parent = parentTrunk;
            newTrunk.Controller = Controller;
            newTrunk.Direction = direction;
            newTrunk.TreeSettings = TreeSettings;
            newTrunk.SegmentCount = 1;

            // Pridej jako dite parenta
            AddChild(parentTrunk, newTrunk);

            // Umisti na mapu
            newTrunk.Init(map);

            // Propoj statickou fyzikou
            parentTrunk.CreateRbJoint(newTrunk).SetupSp();

            // Propaguj segmentCount nahoru
            PropagateSegmentCountChange(parentTrunk, 1);

            // Vytvor vizualni odbocku pokud meni smer
            if (direction != parentTrunk.Direction && direction != parentTrunk.Direction.Opposite())
            {
                CreateBranchVisual(parentTrunk, direction, isDiagonal);
            }
        }

        private void CreateBranchVisual(TreeTrunk onTrunk, Dir8 branchDirection, bool isDiagonal)
        {
            onTrunk.hasBranch = true;

            var prefab = isDiagonal
                ? Game.Instance.PrefabsStore.TreeBranchDg
                : Game.Instance.PrefabsStore.TreeBranch;

            var branch = Game.Instance.Pool.Get(prefab, onTrunk.transform);
            branch.transform.localPosition = Vector3.zero;
            branch.transform.rotation = branchDirection.ToRotation();
        }

        private void ReturnBranchesToPool()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.TryGetComponent<LabelWithSettings>(out var label) && label.Ksid == Core.Ksid.TreeBranch)
                {
                    Game.Instance.Pool.Store(label, label.Prototype);
                }
            }
            hasBranch = false;
        }

        private static void AddChild(TreeTrunk parent, TreeTrunk child)
        {
            child.NextSibling = parent.FirstChild;
            parent.FirstChild = child;
        }

        private static void PropagateSegmentCountChange(TreeTrunk node, int delta)
        {
            while (node != null)
            {
                node.SegmentCount += delta;
                node = node.Parent;
            }
        }

        // Odpojeni ze stromove struktury

        private void RemoveFromParent()
        {
            if (Parent == null)
                return;

            PropagateSegmentCountChange(Parent, -SegmentCount);

            if (Parent.FirstChild == this)
            {
                Parent.FirstChild = NextSibling;
            }
            else
            {
                var sibling = Parent.FirstChild;
                while (sibling != null && sibling.NextSibling != this)
                    sibling = sibling.NextSibling;
                if (sibling != null)
                    sibling.NextSibling = NextSibling;
            }
        }

        private void DetachChildren()
        {
            var child = FirstChild;
            while (child != null)
            {
                var next = child.NextSibling;
                child.Parent = null;
                child.Controller = null;
                child = next;
            }
        }
    }
}
