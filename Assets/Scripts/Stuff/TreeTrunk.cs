using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Core.StaticPhysics;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Stuff
{
    public class TreeTrunk : Placeable, ISimpleTimerConsumer, IHasRbJointCleanup
    {
        // Stromova struktura
        private TreeTrunk Parent;
        private TreeTrunk FirstChild;
        private TreeTrunk NextSibling;
        private Dir8 Direction;
        private bool IsUnderground;

        // Konfigurace (nastaveno na prefabu nebo pri spawnu)
        public TreeSettings TreeSettings;

        // Pocet segmentu: 1 (ja) + pocet vsech potomku
        private int SegmentCount;
        // Balance: kladne = prevaha nadzemi, zaporne = prevaha podzemi
        private float UndergroundBalance;
        private bool isGrowing;
        private bool hasBranch;
        private Collider myCollider;
        private int thicknessLevel = 1;
        private Transform myBranch; // child transform branche ze ktere tento segment vede

        // ISimpleTimerConsumer
        private int timerTag;
        int ISimpleTimerConsumer.ActiveTag { get => timerTag; set => timerTag = value; }

        private bool IsController => Parent == null;

        private void Awake()
        {
            myCollider = GetComponentInChildren<Collider>();
        }

        public override float GetMass()
        {
            return TreeSettings.BaseMass * thicknessLevel * thicknessLevel;
        }

        public override (float StretchLimit, float CompressLimit, float MomentLimit) SpLimits
        {
            get
            {
                float m = thicknessLevel * thicknessLevel;
                return (TreeSettings.BaseSpStretchLimit * m, TreeSettings.BaseSpCompressLimit * m, TreeSettings.BaseSpMomentLimit * m);
            }
        }

        private enum GrowTarget : byte { None, Free, Dirt }

        private struct GrowthCandidate
        {
            public TreeTrunk Source;
            public Dir8 Direction;
            public float Weight;
            public Placeable DirtTarget; // null = nadzemni, jinak hlina v cilove bunce
        }

        private static readonly List<GrowthCandidate> candidates = new List<GrowthCandidate>();

        protected override void AfterMapPlaced(Map.Map map, bool goesFromInventory)
        {
            ApplyThicknessScale(thicknessLevel);

            if (Parent == null)
            {
                // Jsme ridici uzel
                SegmentCount = 1;

                // Zjistime zda jsme v hline
                ref var cell = ref map.GetCell(Pivot);
                var dirt = FindDirt(ref cell);
                if (dirt != null)
                {
                    IsUnderground = true;
                    DisableCollider();
                    var rbj = CreateRbJoint(dirt);
                    rbj.SetupSp();
                    rbj.EnableRbjCleanup();
                } 
                else
                {
                    var segments = ListPool<Placeable>.Rent();
                    segments.Add(this);
                    Physics.SyncTransforms();
                    var connector = new SpConnectionFinder(segments, map);
                    connector.TryConnect();
                    segments.Return();
                }

                UndergroundBalance = IsUnderground ? TreeSettings.UndergroundBalanceWeight : 1f;

                StartGrowth();
            }
        }

        public override void Cleanup(bool goesToInventory)
        {
            StopGrowth();
            RemoveFromParent();
            DetachChildren();
            if (IsUnderground)
                EnableCollider();
            ReturnBranchesToPool();

            NextSibling = null;
            SegmentCount = 0;
            UndergroundBalance = 0;
            IsUnderground = false;
            myBranch = null;
            if (thicknessLevel > 1)
            {
                thicknessLevel = 1;
                ApplyThicknessScale(1);
            }

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

            if (SegmentCount < TreeSettings.MaxSegments)
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
                float roll = Random.Range(0f, totalWeight);
                float cumulative = 0f;
                for (int i = 0; i < candidates.Count; i++)
                {
                    cumulative += candidates[i].Weight;
                    if (roll <= cumulative)
                    {
                        var c = candidates[i];
                        GrowSegment(c.Source, c.Direction, c.Source.Pivot + c.Direction.ToVector(), map, c.DirtTarget);
                        break;
                    }
                }
            }

            candidates.Clear();
        }

        private void CollectCandidates(TreeTrunk node, Map.Map map, ref float totalWeight)
        {
            int cellZ = node.CellZ;

            for (int d = 0; d < 8; d++)
            {
                var dir = (Dir8)d;
                if (node.Parent != null && dir == node.Direction.Opposite())
                    continue;

                TryAddCandidate(node, dir, map, cellZ, ref totalWeight);
            }

            var child = node.FirstChild;
            while (child != null)
            {
                CollectCandidates(child, map, ref totalWeight);
                child = child.NextSibling;
            }
        }

        private void TryAddCandidate(TreeTrunk source, Dir8 dir, Map.Map map, int cellZ, ref float totalWeight)
        {
            var targetPos = source.Pivot + dir.ToVector();
            var growTarget = GetGrowTarget(source, map, targetPos, cellZ, out var dirtTarget);
            if (growTarget == GrowTarget.None)
                return;

            bool direct = dir == source.Direction || dir == source.Direction.Opposite();
            bool targetUnderground = growTarget == GrowTarget.Dirt;

            float weight = 1;

            weight *= GetBalanceWeight(targetUnderground);
            if (weight <= 0)
                return;

            if (!targetUnderground && !direct)
            {
                weight *= TreeSettings.BranchWeight;
                if (HasNearbyBranch(source))
                    weight *= TreeSettings.NearBranchWeight;
                if (source.FirstChild == null)
                    weight *= TreeSettings.EndBranchPenalty;
            }

            // Smerova vaha
            if (targetUnderground)
                weight *= dir.Opposite().UpWeight(TreeSettings.RootUpExponent);
            else
                weight *= dir.UpWeight(TreeSettings.UpExponent);

            // Volny prostor - jen nadzemni
            if (!targetUnderground)
            {
                weight *= GetFreeNeighborWeight(map, targetPos, cellZ);
            }

            weight *= GetCenterWeight(targetPos.x);

            if (weight <= 0)
                return;

            candidates.Add(new GrowthCandidate
            {
                Source = source,
                Direction = dir,
                Weight = weight,
                DirtTarget = dirtTarget,
            });
            totalWeight += weight;
        }

        private GrowTarget GetGrowTarget(TreeTrunk source, Map.Map map, Vector2 targetPos, int cellZ, out Placeable dirtTarget)
        {
            dirtTarget = null;
            ref var cell = ref map.GetCell(targetPos);
            bool hasPart = cell.Blocking.HasSubFlag(SubCellFlags.Part, cellZ);

            if (!hasPart)
            {
                // Volna bunka - nadzemni rust
                return GrowTarget.Free;
            }

            // Nadzemni source nemuze prechazet do zeme (krome controlleru)
            if (!source.IsUnderground && !source.IsController)
                return GrowTarget.None;

            // Bunka je obsazena - zkusime najit hlinu

            // Kontrola zda bunka neobsahuje neco co blokuje koreny (napr. jiny TreeTrunk)
            if (map.ContainsType(ref cell, Ksid.BlocksTreeRoots))
                return GrowTarget.None;

            dirtTarget = FindDirt(ref cell);
            return dirtTarget != null ? GrowTarget.Dirt : GrowTarget.None;
        }

        private static Placeable FindDirt(ref Map.Cell cell)
        {
            foreach (var p in cell)
            {
                if (p.Ksid.IsChildOfOrEq(Ksid.Dirt) && !p.HasActiveRB)
                    return p;
            }
            return null;
        }

        private float GetBalanceWeight(bool targetUnderground)
        {
            float balance = UndergroundBalance * TreeSettings.BalanceWeight / SegmentCount;
            // balance > 0 = prevaha nadzemi -> preferovat podzemi
            // balance < 0 = prevaha podzemi -> preferovat nadzemi
            if (targetUnderground)
                balance *= -1;

            return Mathf.Clamp01(1 - balance);
        }

        private static bool HasNearbyBranch(TreeTrunk node)
        {
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

        private void GrowSegment(TreeTrunk parentTrunk, Dir8 direction, Vector2 targetPos, Map.Map map, Placeable dirtTarget)
        {
            var pos = new Vector3(targetPos.x, targetPos.y, parentTrunk.transform.position.z);
            bool isDiagonal = direction.IsDiagonal();
            bool underground = dirtTarget != null;
            bool direct = direction == parentTrunk.Direction || direction == parentTrunk.Direction.Opposite();

            var prefab = isDiagonal
                ? Game.Instance.PrefabsStore.TreeTrunkDg
                : Game.Instance.PrefabsStore.TreeTrunk;

            var newTrunk = prefab.CreateWithotInit(LevelGroup, pos);

            newTrunk.transform.rotation = direction.ToRotation();

            // Nastav stromovou strukturu pred Init
            newTrunk.Parent = parentTrunk;
            newTrunk.Direction = direction;
            newTrunk.TreeSettings = TreeSettings;
            newTrunk.IsUnderground = underground;
            newTrunk.SegmentCount = 1;
            if (direct && parentTrunk.thicknessLevel > 1)
                newTrunk.thicknessLevel = parentTrunk.thicknessLevel - 1;
            float balanceDelta = underground ? TreeSettings.UndergroundBalanceWeight : 1f;
            newTrunk.UndergroundBalance = balanceDelta;

            AddChild(parentTrunk, newTrunk);

            // Umisti na mapu
            newTrunk.Init(map);

            // Propoj statickou fyzikou s parentem
            var rbj = parentTrunk.CreateRbJoint(newTrunk);
            rbj.SetupSp();
            rbj.OtherConnectable.EnableRbjCleanup();

            // Podzemni: deaktivuj collider a propoj s hlinou
            if (underground)
            {
                newTrunk.DisableCollider();
                var dirtRbj = newTrunk.CreateRbJoint(dirtTarget);
                dirtRbj.SetupSp();
                dirtRbj.EnableRbjCleanup();
            }

            PropagateCountChange(parentTrunk, 1, balanceDelta);

            // Vizualni odbocka
            if (!direct)
            {
                CreateBranchVisual(newTrunk, parentTrunk, direction, isDiagonal);
            }
        }

        private void DisableCollider()
        {
            if (myCollider) myCollider.enabled = false;
        }

        private void EnableCollider()
        {
            if (myCollider) myCollider.enabled = true;
            if (hasBranch)
            {
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    var child = transform.GetChild(i);
                    if (child.TryGetComponent<LabelWithSettings>(out var label) && label.Ksid == Ksid.TreeBranch)
                    {
                        var col = label.GetComponentInChildren<Collider>();
                        col.enabled = true;
                    }
                }
            }
        }

        private void CreateBranchVisual(TreeTrunk childTrunk, TreeTrunk onTrunk, Dir8 branchDirection, bool isDiagonal)
        {
            onTrunk.hasBranch = true;

            var prefab = isDiagonal
                ? Game.Instance.PrefabsStore.TreeBranchDg
                : Game.Instance.PrefabsStore.TreeBranch;

            var branch = Game.Instance.Pool.Get(prefab, onTrunk.transform);
            branch.transform.localPosition = Vector3.zero;
            branch.transform.rotation = branchDirection.ToRotation();

            // Scale na child objektu branche (mesh), podle levelu syna
            var meshTransform = branch.transform.GetChild(0);
            ApplyThicknessScale(meshTransform, childTrunk.thicknessLevel);

            // Syn si pamatuje svou branch
            childTrunk.myBranch = meshTransform;

            if (onTrunk.IsUnderground)
            {
                var col = branch.GetComponentInChildren<Collider>();
                if (col) col.enabled = false;
            }
        }

        private void ReturnBranchesToPool()
        {
            if (hasBranch)
            {
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    var child = transform.GetChild(i);
                    if (child.TryGetComponent<LabelWithSettings>(out var label) && label.Ksid == Ksid.TreeBranch)
                    {
                        var col = label.GetComponentInChildren<Collider>();
                        col.enabled = true;
                        ApplyThicknessScale(col.transform, 1);
                        Game.Instance.Pool.Store(label, label.Prototype);
                    }
                }
                hasBranch = false;
            }
        }

        private static void AddChild(TreeTrunk parent, TreeTrunk child)
        {
            child.NextSibling = parent.FirstChild;
            parent.FirstChild = child;
        }

        private void PropagateCountChange(TreeTrunk node, int segmentDelta, float balanceDelta)
        {
            while (node != null)
            {
                node.SegmentCount += segmentDelta;
                node.UndergroundBalance += balanceDelta;

                int newLevel = TreeSettings.GetThicknessLevel(node.SegmentCount);
                if (newLevel > node.thicknessLevel)
                {
                    node.UpdateThickness(newLevel);
                }

                node = node.Parent;
            }
        }

        // Tloustka

        private void UpdateThickness(int newLevel)
        {
            float oldMass = GetMass();
            thicknessLevel = newLevel;
            float newMass = GetMass();

            PropagateLevelDiscontinuity();

            // Vizualni scale na child objektu (mesh + collider)
            ApplyThicknessScale(newLevel);

            // SP: aktualizuj hmotnost (delta sily)
            if (SpNodeIndex != 0)
            {
                var sp = Game.Instance.StaticPhysics;
                sp.ApplyForce(SpNodeIndex, Vector2.down * (newMass - oldMass));

                // SP: aktualizuj limity vsech jointu tohoto uzlu
                UpdateJointLimits(sp);
            }
        }

        private void PropagateLevelDiscontinuity()
        {
            var child = FirstChild;
            while (child != null)
            {
                if (child.myBranch == null && thicknessLevel > child.thicknessLevel + 1)
                {
                    child.UpdateThickness(thicknessLevel - 1);
                }
                child = child.NextSibling;
            }
        }

        private void ApplyThicknessScale(int level)
        {
            ApplyThicknessScale(myCollider.transform, level);
            if (myBranch != null)
                ApplyThicknessScale(myBranch, level);
        }

        private void ApplyThicknessScale(Transform t, int level)
        {
            float scale = TreeSettings.ScaleConstant + (level - 1) * TreeSettings.ScalePerLevel;
            var ls = t.localScale;
            ls.x = scale;
            ls.z = scale;
            t.localScale = ls;
        }

        private void UpdateJointLimits(SpInterface sp)
        {
            var limits = SpLimits;

            // Joint s parentem
            if (Parent != null && Parent.SpNodeIndex != 0)
            {
                var parentLimits = Parent.SpLimits;
                sp.UpdateJointLimits(SpNodeIndex, Parent.SpNodeIndex,
                    Mathf.Min(limits.StretchLimit, parentLimits.StretchLimit),
                    Mathf.Min(limits.CompressLimit, parentLimits.CompressLimit),
                    Mathf.Min(limits.MomentLimit, parentLimits.MomentLimit));
            }

            // Jointy s detmi
            var child = FirstChild;
            while (child != null)
            {
                if (child.SpNodeIndex != 0)
                {
                    var childLimits = child.SpLimits;
                    sp.UpdateJointLimits(SpNodeIndex, child.SpNodeIndex,
                        Mathf.Min(limits.StretchLimit, childLimits.StretchLimit),
                        Mathf.Min(limits.CompressLimit, childLimits.CompressLimit),
                        Mathf.Min(limits.MomentLimit, childLimits.MomentLimit));
                }
                child = child.NextSibling;
            }
        }

        // Reakce na pretrzeni vazby

        void IHasRbJointCleanup.RbJointCleanup(RbJoint rbj)
        {
            if (rbj.OtherObj is TreeTrunk)
            {
                // Pretrzeni trunk-trunk: tento uzel je syn, odpojit od parenta
                RemoveFromParent();
            }
            else
            {
                // Pretrzeni trunk-hlina: zapnout collider
                if (IsUnderground)
                    EnableCollider();
            }
        }


        // Odpojeni ze stromove struktury

        private void RemoveFromParent()
        {
            if (Parent == null)
                return;

            PropagateCountChange(Parent, -SegmentCount, -UndergroundBalance);

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
            Parent = null;
            myBranch = null;
        }

        private void DetachChildren()
        {
            var child = FirstChild;
            FirstChild = null;
            while (child != null)
            {
                var next = child.NextSibling;
                child.NextSibling = null;
                child.Parent = null;
                child.myBranch = null;
                child = next;
            }
        }
    }
}
