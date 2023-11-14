using Assets.Scripts.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Utils
{
    internal struct SpConnectionFinder
    {
        private readonly int tag;
        private bool connected;
        private readonly List<Placeable> nodes;
        private readonly List<Placeable> candidates;
        private readonly Ksids ksids;

        private const int step3 = 3;

        private enum CmpRes
        {
            Uncomparable,
            FirstOut,
            SecondOut,
            FirstOutWithHope,
            SecondOutWithHope,
        }


        public SpConnectionFinder(List<Placeable> nodes)
        {
            tag = Game.Map.GetNextTag();
            this.nodes = nodes;
            candidates = ListPool<Placeable>.Rent();
            connected = false;
            ksids = Game.Instance.Ksids;
        }

        public void TryConnect()
        {
            GiveTag();

            int i = 0;
            int lastI = 0;

            while (i < nodes.Count)
            {
                if (TryConnectOne(nodes[i]))
                {
                    connected = true;
                    if (i - lastI > step3 * 2)
                        TryConnctGap(lastI, i);
                    lastI = i;
                }

                if (i + step3 > nodes.Count && i + 1 < nodes.Count)
                {
                    i = nodes.Count - 1;
                }
                else
                {
                    i += step3;
                }
            }

            if (nodes.Count - 1 - lastI > step3 * 2)
                TryConnctGap(lastI, nodes.Count - 1);

            CreateJoints();

            candidates.Return();
        }


        private void TryConnctGap(int start, int end)
        {
            do
            {
                start++;
                end--;
                if (start % step3 != 0)
                {
                    if (TryConnectOne(nodes[start]))
                    {
                        start += step3 * 2 - 1;
                        connected = true;
                    }
                }

                if (end % step3 != 0)
                {
                    if (TryConnectOne(nodes[end]))
                    {
                        end -= step3 * 2 - 1;
                        connected = true;
                    }
                }
            }
            while (start < end - 1);
        }

        private bool TryConnectOne(Placeable p)
        {
            p.FindTouchingObjs(candidates, Ksid.SpNode, 0.05f, tag);
            Vector2 center = p.Center;

            int size = candidates.Count;

            for ( int i = 0; i < size; )
            {
                if (candidates[i].HasActiveRB)
                {
                    Swap(i, ref size);
                    goto NextRound;
                }

                for (int j = i + 1; j < size; )
                {
                    CmpRes res = Compare(i, j, center);
                    switch (res)
                    {
                        case CmpRes.Uncomparable:
                            j++;
                            break;
                        case CmpRes.FirstOut:
                            Swap(i, ref size);
                            goto NextRound;
                        case CmpRes.FirstOutWithHope:
                            candidates[i].Tag = 0;
                            Swap(i, ref size);
                            goto NextRound;
                        case CmpRes.SecondOut:
                            Swap(j, ref size);
                            break;
                        case CmpRes.SecondOutWithHope:
                            candidates[j].Tag = 0;
                            Swap(j, ref size);
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                }
                i++;

                NextRound:;
            }

            ConnectToOthers(p, size);

            candidates.Clear();
            return size > 0;
        }


        private CmpRes Compare(int i, int j, Vector2 center)
        {
            Placeable c1 = candidates[i];
            Placeable c2 = candidates[j];
            if (c2.HasActiveRB)
                return CmpRes.SecondOut;

            Vector2 dir1 = c1.Center - center;
            Vector2 dir2 = c2.Center - center;
            if (Vector2.Dot(dir1, dir2) <= 0)
                return CmpRes.Uncomparable;

            bool fixed1 = ksids.IsParentOrEqual(c1.Ksid, Ksid.SpFixed);
            bool fixed2 = ksids.IsParentOrEqual(c2.Ksid, Ksid.SpFixed);

            if (!fixed1 && fixed2)
                return CmpRes.FirstOutWithHope;
            if (!fixed2 && fixed1)
                return CmpRes.SecondOutWithHope;

            float diff = dir1.sqrMagnitude - dir2.sqrMagnitude;
            if (diff < 0)
                return CmpRes.SecondOutWithHope;
            if (diff > 0)
                return CmpRes.FirstOutWithHope;

            return CmpRes.Uncomparable;
        }

        private void Swap(int i, ref int size)
        {
            candidates[i] = candidates[size - 1];
            size--;
        }

        private void GiveTag()
        {
            foreach (var n in nodes)
                n.Tag = tag;
        }


        private void ConnectToOthers(Placeable p, int size)
        {
            for (int i = 0; i < size; i++)
            {
                var c = candidates[i];
                p.CreateRbJoint(c).SetupSp();
            }
        }


        private void CreateJoints()
        {
            for (int i = 0; i < nodes.Count -1; i++)
            {
                var j = nodes[i].CreateRbJoint(nodes[i+1]);
                if (connected)
                    j.SetupJoint();
                else
                    j.SetupSp();
            }
        }
    }
}
