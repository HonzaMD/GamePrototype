using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static Assets.Scripts.Map.Visibility.DarkBorders;

namespace Assets.Scripts.Map.Visibility
{
    internal class DarkBorders
    {
        private readonly List<Border>[] borders = new List<Border>[4];
        private int count;

        public DarkBorders()
        {
            borders[0] = new();
            borders[1] = new();
            borders[2] = new();
            borders[3] = new();
        }

        private readonly struct Border
        {
            public readonly Vector2 Dir;
            public readonly DarkCaster DC;
            public readonly bool IsLeft;

            public Border(Vector2 dir, DarkCaster dc, bool isLeft)
            {
                Dir = dir;
                DC = dc;
                IsLeft = isLeft;
            }

            public Vector2 Point => IsLeft ? DC.LeftPoint : DC.RightPoint;

            public override string ToString()
            {
                return $"{(IsLeft?'L':'R')} {Dir.normalized}";
            }
        }

        public readonly struct BorderPtr
        {
            public readonly sbyte arr;
            public readonly bool exact;
            public readonly sbyte relArrDist;
            public readonly int pos;
            public bool IsNull => arr == -1;
            public int AbsRelArrDist => Mathf.Abs(relArrDist);

            public BorderPtr(int arr, int pos, bool exact, int relArrDist)
            {
                this.arr = (sbyte)arr;
                this.pos = pos;
                this.exact = exact;
                this.relArrDist = (sbyte)relArrDist;
            }

            public bool PosEq(BorderPtr other) => arr == other.arr && pos == other.pos;

            public static BorderPtr Null = new BorderPtr(-1, 0, false, 10);

            public override string ToString()
            {
                return $"{arr}:{pos} {(exact?"EXACT":"")} ({relArrDist})";
            }
        }

        public bool Add(DarkCaster dc)
        {
            if (count == 0)
            {
                InsetrTwo(new Border(dc.LeftDir, dc, true), new Border(dc.RightDir, dc, false), BorderPtr.Null);
                return true;
            }
            else
            {
                var left = Find(dc.LeftDir);
                var right = Find(dc.RightDir);

                int cnt = Count(left, right);

                bool doLeftTest = false;
                bool doRightTest = false;

                if (cnt == 0)
                {
                    if (!Get(right).IsLeft)
                        //throw new InvalidOperationException("DC se cely prekryva");
                        return false;
                    doLeftTest = true;
                    doRightTest = true;
                    left = GoLeft(left);
                }
                else if (cnt == 1) 
                { 
                    if (Get(left).IsLeft)
                    {
                        doLeftTest = true;
                        right = left;
                        left = GoLeft(left);
                    } 
                    else
                    {
                        doRightTest= true;
                        right = GoRight(left);
                    }
                }
                else if (cnt == 2) 
                {
                    if (Get(left).IsLeft)
                        //throw new InvalidOperationException("DC se obsahuje cely jiny DC");
                        return false;
                    right = GoRight(left);
                } 
                else
                {
                    throw new InvalidOperationException("DC obsahuje vic DC");
                }

                var collapseLeft = !doLeftTest || TestCollapse(left, dc);
                var collapseRight = !doRightTest || TestCollapse(right, dc);

                if (dc.connectsLeft && !collapseLeft)
                    Debug.Log("Divnost: Byl jsem spojenej doleva");
                if (dc.connectsRight && !collapseRight)
                    Debug.Log("Divnost: Byl jsem spojenej doprava");

                if (collapseLeft)
                    DarkGroup.Join(Get(left).DC, dc);
                if (collapseRight)
                    DarkGroup.Join(dc, Get(right).DC);

                if (!collapseLeft && !collapseRight)
                    InsetrTwo(new Border(dc.LeftDir, dc, true), new Border(dc.RightDir, dc, false), right);
                else if (collapseLeft && collapseRight)
                    RemoveTwoAt(left, right);
                else if (collapseLeft)
                    Swap(left, new Border(dc.RightDir, dc, false));
                else
                    Swap(right, new Border(dc.LeftDir, dc, true));

                return true;
            }
        }

        private void Swap(BorderPtr ptr, Border border)
        {
            int arr = DirToArr(border.Dir);
            if (arr == ptr.arr)
            {
                borders[arr][ptr.pos] = border;
            }
            else 
            {
                var destArr = borders[arr];
                if (destArr.Count == 0 || !VCore.IsBetterOrder(destArr[destArr.Count-1].Dir, border.Dir))
                {
                    destArr.Add(border);
                }
                else
                {
                    destArr.Insert(0, border);
                }

                borders[ptr.arr].RemoveAt(ptr.pos);
            }
        }

        private void RemoveTwoAt(BorderPtr ptr1, BorderPtr ptr2)
        {
            count -= 2;
            if (ptr1.arr == ptr2.arr && ptr1.pos + 1 == ptr2.pos)
            {
                borders[ptr1.arr].RemoveRange(ptr1.pos, 2);
            }
            else
            {
                borders[ptr1.arr].RemoveAt(ptr1.pos);
                borders[ptr2.arr].RemoveAt(ptr2.pos);
            }
        }


        private void InsetrTwo(Border border1, Border border2, BorderPtr ptr)
        {
            count += 2;
            if (ptr.IsNull)
            {
                borders[DirToArr(border1.Dir)].Add(border1);
                borders[DirToArr(border2.Dir)].Add(border2);
            }
            else
            {
                int arr = DirToArr(border1.Dir);
                if (arr != ptr.arr)
                {
                    borders[arr].Add(border1);

                    arr = DirToArr(border2.Dir);
                    if (arr != ptr.arr)
                    {
                        borders[arr].Add(border2);
                    }
                    else
                    {
                        InflateArr(borders[arr], ptr.pos, 1);
                        borders[arr][ptr.pos] = border2;
                    }
                } 
                else
                {
                    InflateArr(borders[arr], ptr.pos, 2);
                    borders[arr][ptr.pos] = border1;
                    borders[arr][ptr.pos + 1] = border2;
                }
            }
        }

        private void InflateArr(List<Border> arr, int pos, int shift)
        {
            for (int f = 0; f < shift; f++)
            {
                arr.Add(arr.Count - shift >= 0 ? arr[arr.Count - shift] : default);
            }
            for (int f = arr.Count - shift - 1; f - shift >= pos; f--)
            {
                arr[f] = arr[f - shift];
            }
        }

        private bool TestCollapse(BorderPtr ptr, DarkCaster dc)
        {
            if (ptr.AbsRelArrDist > 1)
                return false;

            var border = Get(ptr);
            Vector2 leftDir, rightDir, leftPoint, rightPoint;
            if (border.IsLeft)
            {
                leftDir = border.Dir;
                leftPoint = border.DC.LeftPoint;
                rightDir = dc.RightDir;
                rightPoint = dc.RightPoint;
            }
            else
            {
                leftDir = dc.LeftDir;
                leftPoint = dc.LeftPoint;
                rightDir = border.Dir;
                rightPoint = border.DC.RightPoint;
            }

            Vector2 leftNormal = VCore.TurnLeft(leftDir);
            Vector2 rightNormalNeg = VCore.TurnLeft(rightDir);
            Vector2 rTol = leftPoint - rightPoint;

            if (!VCore.IsBetterOrder(leftDir, rightDir) && Vector2.Dot(leftDir, rightDir) <= 0)
                Debug.LogError("otoceny TestCollapse");

            // test zda se poloprimky krizi
            if (Vector2.Dot(leftNormal, rTol) >= 0 && Vector2.Dot(rightNormalNeg, rTol) >= 0)
            {
                if (border.IsLeft)
                {
                    if (Vector2.Dot(leftDir, rTol) <= 0)
                        dc.CorrectRightDir(leftDir);
                    else
                        border.DC.CorrectLeftDir(rightDir);
                }
                else
                {
                    if (Vector2.Dot(rightDir, rTol) >= 0)
                        dc.CorrectLeftDir(rightDir);
                    else
                        border.DC.CorrectRightDir(leftDir);
                }

                return true;
            }
            
            return false;
        }

        private BorderPtr Find(Vector2 dir)
        {
            int arrPtr = DirToArr(dir);
            var arr = borders[arrPtr];
            int pos = 0;
            for (; ; )
            {
                if (pos == arr.Count)
                    return NextArr(arrPtr, 1, 0, false);
                float cp = VCore.CrossProduct(dir, arr[pos].Dir);
                if (Mathf.Abs(cp) < 0.00001f)
                {
                    return new BorderPtr(arrPtr, pos, true, 0);
                }
                else if (cp > 0)
                {
                    pos++;
                }
                else
                {
                    return new BorderPtr(arrPtr, pos, false, 0);
                }
            }
        }

        public (Vector2 left, Vector2 right, bool leftOk, bool rightOK) FindLeftRight(Vector2 dir)
        {
            var ptr = Find(dir);
            var border = Get(ptr);
            if (border.IsLeft)
            {
                var ptrR = GoRight(ptr);
                var borderR = Get(ptrR);
                return (border.Dir, borderR.Dir, ptr.AbsRelArrDist <= 1, ptrR.AbsRelArrDist <= 1);
            }
            else
            {
                var ptrL = GoLeft(ptr);
                var borderL = Get(ptrL);
                return (borderL.Dir, border.Dir, ptrL.AbsRelArrDist <= 1, ptr.AbsRelArrDist <= 1);
            }
        }

        public static int DirToArr(Vector2 dir)
        {
            if (Mathf.Abs(dir.y) > Mathf.Abs(dir.x))
            {
                return dir.y > 0 ? 0 : 2;
            }
            else
            {
                return dir.x > 0 ? 1 : 3;
            }
        }

        private Border Get(BorderPtr ptr) => borders[ptr.arr][ptr.pos];

        private BorderPtr GoLeft(BorderPtr ptr)
        {
            if (ptr.pos == 0)
            {
                return NextArr(ptr.arr, -1, ptr.relArrDist, true);
            }
            else
            {
                return new BorderPtr(ptr.arr, ptr.pos - 1, true, ptr.relArrDist);
            }
        }

        private BorderPtr GoRight(BorderPtr ptr)
        {
            if (borders[ptr.arr].Count == ptr.pos + 1)
            {
                return NextArr(ptr.arr, 1, ptr.relArrDist, true);
            }
            else
            {
                return new BorderPtr(ptr.arr, ptr.pos + 1, true, ptr.relArrDist);
            }
        }

        private BorderPtr NextArr(int arr, int dir, int relDist, bool exact)
        {
            if (count == 0)
                throw new InvalidOperationException("DarkBorders je prazdne");
            while (true)
            {
                arr += dir;
                relDist += dir;
                if (arr > 3)
                    arr = 0;
                if (arr < 0)
                    arr = 3;
                if (borders[arr].Count > 0)
                {
                    return new BorderPtr(arr, dir > 0 ? 0 : borders[arr].Count - 1, exact, relDist);
                }
            }
        }

        private int Count(BorderPtr fromLeft, BorderPtr toRight)
        {
            int count = 0;
            if (toRight.exact)
                count++;

            while (!fromLeft.PosEq(toRight))
            {
                fromLeft = GoRight(fromLeft);
                count++;
            }

            return count;
        }

        public short StartDrawing(Vector2 dir, out BorderPtr nextPtr, out Vector2 nextDir, out Vector2 point)
        {
            if (count == 0)
            {
                nextPtr = BorderPtr.Null;
                nextDir = -dir;
                point = Vector2.zero;
                return -1;
            }
            nextPtr = Find(dir);
            var border = Get(nextPtr);
            nextDir = border.Dir;
            point = border.Point;
            return border.IsLeft ? (short)-1 : border.DC.Id;
        }

        public short ContinueDrawing(ref BorderPtr ptr, out Vector2 nextDir, out Vector2 point)
        {
            ptr = GoRight(ptr);
            var border = Get(ptr);
            nextDir = border.Dir;
            point = border.Point;
            return border.IsLeft ? (short)-1 : border.DC.Id;
        }


        public void Clear() 
        {
            count = 0;
            foreach (var b in borders) 
                b.Clear();
        }
    }
}
