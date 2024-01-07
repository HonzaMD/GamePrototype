using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Map
{
    public partial class Visibility
    {

        internal class ShadowWorker
        {
            private readonly Visibility visibility;
            private Vector2 centerPosAdj;
            private Vector2Int up;
            private Vector2Int right;
            private int shift;
            private Vector2Int pos;
            private Vector2 posF;
            private Vector2Int posLS;
            private Vector2 toEnd;
            private int xDist;
            private int yDist;

            public ShadowWorker(Visibility visibility)
            {
                this.visibility = visibility;
            }

            public bool ResolvePartShadow(Vector2Int pos)
            {
                xDist = Math.Abs(pos.x - HalfXSize);
                yDist = Math.Abs(pos.y - HalfYSize);
                var toCenter = new Vector2Int(Math.Sign(HalfXSize - pos.x), Math.Sign(HalfYSize - pos.y));
                
                centerPosAdj = visibility.centerPosLocal - (Vector2)pos * 0.5f;
                centerPosAdj = new Vector2(Mathf.Abs(centerPosAdj.x), Mathf.Abs(centerPosAdj.y));
                this.pos = pos;

                if (yDist < xDist)
                {
                    shift = 1;
                    (xDist, yDist) = (yDist, xDist);
                    centerPosAdj = new Vector2(centerPosAdj.y, centerPosAdj.x);
                    up = new Vector2Int(toCenter.x, 0);
                    right = new Vector2Int(0, toCenter.y);
                }
                else
                {
                    shift = 0;
                    up = new Vector2Int(0, toCenter.y);
                    right = new Vector2Int(toCenter.x, 0);
                }

                // ted plati ze yDist >= xDist


                if (xDist == 0)
                {
                    if (HasFloor(Vector2Int.zero))
                        return true;
                    if (HasFloor(Vector2Int.up) && yDist > 1)
                        return true;
                    if (HasFloor(Vector2Int.up * 2) && yDist > 2)
                        return true;
                    return false;
                }
                else
                {
                    posF = new Vector2(-0.25f, 0.25f);
                    posLS = Vector2Int.left;
                    bool result = false;
                    toEnd = new Vector2(0.25f, -0.25f) - centerPosAdj;

                    int counter = 0;
                    while (ShadowStep(ref result))
                    {
                        counter++;
                        if (counter > 10)
                            throw new InvalidOperationException("cyklus v ShadowStep");
                    }

                    return result;
                }
            }

            private bool ShadowStep(ref bool result)
            {
                var toPosF = posF - centerPosAdj;
                if (IsInOrder(toEnd, toPosF))
                {
                    result = true;
                    return false;
                }

                if (posLS.x < 2 && posLS.x + 1 <= xDist && HasFloor(posLS + Vector2Int.right))
                {
                    posLS = posLS + Vector2Int.right;
                    posF.x += 0.5f;
                }
                else if (posLS.x >= 0 && posLS.y >= 0 && posLS.x < xDist && HasSide(posLS))
                {
                    posLS = posLS + Vector2Int.down;
                    posF.y -= 0.5f;
                }
                else if (!FindNewLine(toPosF))
                {
                    return false;
                }
                return true;
            }

            private bool FindNewLine(Vector2 toPosF)
            {
                float xStep = toPosF.x / toPosF.y;

                Vector2Int p2 = posLS + Vector2Int.one;
                Vector2 posF2 = posF + new Vector2(0.5f, 0);
                float dx = 0;

                for (; ; )
                {
                    if (p2.x > xDist || p2.y >= yDist)
                        break;
                    
                    dx += xStep;
                    if (dx >= 1)
                    {
                        if (HasSide(p2))
                        {
                            posLS = p2 + Vector2Int.down;
                            posF = posF2;
                            return true;
                        }
                        p2 = p2 + Vector2Int.right;
                        posF2.x += 0.5f;
                    }

                    posF2.y += 0.5f;

                    if (HasFloor(p2))
                    {
                        posLS = p2;
                        posF = posF2;
                        return true;
                    }

                    p2 = p2 + Vector2Int.up;
                }


                p2 = posLS;
                posF2 = posF - new Vector2(0, 0.5f);
                dx = 0;

                for (; ; )
                {
                    dx += xStep;
                    if (dx >= 1)
                    {
                        p2 = p2 + Vector2Int.left;
                        posF2.x -= 0.5f;
                        if (p2.x < 0)
                            break;

                        if (HasSide(p2))
                        {
                            posLS = p2 + Vector2Int.down;
                            posF = posF2;
                            return true;
                        }
                    }

                    p2 = p2 + Vector2Int.down;
                    if (p2.y < 0)
                        break;

                    if (HasFloor(p2))
                    {
                        posLS = p2;
                        posF = posF2;
                        return true;
                    }

                    posF2.y -= 0.5f;
                }

                return false;
            }

            private bool HasFloor(Vector2Int offset) => visibility.Get(pos + right * offset.x + up * offset.y).IsFloor(shift);
            private bool HasSide(Vector2Int offset) => visibility.Get(pos + right * offset.x + up * offset.y).IsSide(shift);

            private bool IsInOrder(Vector2 first, Vector2 second) => first.x * second.y - first.y * second.x >= 0;
        }
    }
}
