using Assets.Scripts.Bases;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
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

        // ISimpleTimerConsumer
        private int timerTag;
        int ISimpleTimerConsumer.ActiveTag { get => timerTag; set => timerTag = value; }

        private bool IsController => Controller == this;

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
            timerTag++; // zneplatni naplanovany timer
        }

        void ISimpleTimerConsumer.OnTimer()
        {
            if (HasActiveRB)
            {
                // Presli jsme na RB fyziku, zastavujeme rust
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
            // Zatim jednoduchy rust: najdi koncovy uzel a prodluz nahoru
            var leaf = FindLeaf();
            if (leaf == null)
                return;

            var targetPos = leaf.Pivot + Dir8.Up.ToVector();
            var map = leaf.GetMap();

            if (!IsCellFree(map, targetPos, leaf.CellZ))
                return;

            GrowSegment(leaf, Dir8.Up, targetPos, map);
        }

        private TreeTrunk FindLeaf()
        {
            // Najdi prvni koncovy uzel (uzel bez deti)
            return FindLeafRecursive(this);
        }

        private TreeTrunk FindLeafRecursive(TreeTrunk node)
        {
            if (node.FirstChild == null)
                return node;
            return FindLeafRecursive(node.FirstChild);
        }

        private bool IsCellFree(Map.Map map, Vector2 targetPos, int cellZ)
        {
            var blocking = map.GetCellBlocking(targetPos);
            return !blocking.HasSubFlag(SubCellFlags.Part, cellZ);
        }

        private void GrowSegment(TreeTrunk parentTrunk, Dir8 direction, Vector2 targetPos, Map.Map map)
        {
            var levelGroup = LevelGroup2;
            var pos = new Vector3(targetPos.x, targetPos.y, parentTrunk.transform.position.z);

            var newTrunk = Game.Instance.PrefabsStore.TreeTrunk.CreateWithotInit(levelGroup.transform, pos);

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

            // Odecti sebe a sve potomky z rodicovske vetve
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
