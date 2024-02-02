using System.Collections.Generic;

namespace Assets.Scripts.Map.Visibility
{
    internal class DCGroup
    {
        private List<DarkCaster> members = new();
        public DarkCaster LeftDC;
        public DarkCaster RightDC;
        private static Stack<DCGroup> pool = new ();

        public static DCGroup Join(DarkCaster left, DarkCaster right)
        {
            DCGroup group = left.Group ?? right.Group ?? Create();
            group.LeftDC = left.Group?.LeftDC ?? left;
            group.RightDC = right.Group?.RightDC ?? right;

            JoinSide(group, left);
            JoinSide(group, right);
            return group;
        }

        private static void JoinSide(DCGroup group, DarkCaster dc)
        {
            if (dc.Group == null)
            {
                group.members.Add(dc);
                dc.Group = group;
            }
            else if (dc.Group != group)
            {
                var oldGroup = dc.Group;
                foreach (var dc2 in oldGroup.members)
                {
                    dc2.Group = group;
                    group.members.Add(dc2);
                }
                Free(oldGroup);
            }
        }

        private static void Free(DCGroup oldGroup)
        {
            oldGroup.members.Clear();
            oldGroup.LeftDC = null;
            oldGroup.RightDC = null;
            pool.Push(oldGroup);
        }

        public void Free()
        {
            foreach (var dc in members)
            {
                dc.Group = null;
            }
            Free(this);
        }

        private static DCGroup Create()
        {
            return pool.Count > 0 ? pool.Pop() : new DCGroup();
        }
    }
}
