using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Assets.Scripts.Map.Visibility
{
    public class CyclusException : InvalidOperationException
    {
        public CyclusException(short dcId) : base("cyclus")
        {
            DcId = dcId;
        }

        public short DcId { get; }
    }

    internal class MeshBuilderWorker
    {
        private static readonly (Vector2 point, Vector2Int lOut, Vector2Int rOut)[] pointsDesc = new [] {
                (new Vector2(-0.05f, -0.05f), Vector2Int.right, Vector2Int.up),
                (new Vector2(0.55f, -0.05f), Vector2Int.up, Vector2Int.left),
                (new Vector2(0.55f, 0.55f), Vector2Int.left, Vector2Int.down),
                (new Vector2(-0.05f, 0.55f), Vector2Int.down, Vector2Int.right)
            };

        private struct PointDesc
        {
            public ushort index;
            public Vector2 point;
            public ushort workNode;

            public PointDesc(Vector2 point, int index, ushort workNode = 0)
            {
                this.point = point;
                this.index = (ushort)index;
                this.workNode = workNode;
            }
        }

        private struct Work
        {
            public ushort pointNode;
            public float sqrDist;

            public Work(ushort pointNode)
            {
                this.pointNode = pointNode;
                sqrDist = 0;
            }
        }

        private readonly VCore core;
        private DarkCaster dc;
        private short markId;

        Vector2 leftPoint, rightPoint;
        Vector2Int startDir, endDir;
        Vector2Int leftCell, rightCell;
        ushort leftPNode, rightPNode; // ptr do pointNodes
        private int rightPIndex; // index do points

        private readonly List<Vector2> points = new(); // vystupni vrcholy. Bez Z, predstavuji predni starnu, zadni se vytvori duplikaci
        private readonly List<ushort> triangles = new(); // vystupni trijuhelniky
        private readonly LinkedListM<PointDesc> pointNodes = new(); // vrcholy k triangulaci
        private readonly LinkedListM<Work> workNodes = new(); // pro prvni cast (FindFrontEdge), kde se vybira od nejmensich prepon

        private float minX, minY, maxX, maxY; // vypocet bounds


        public MeshBuilderWorker(VCore core)
        {
            this.core = core;
        }

        public void Build(DarkCaster dc, int dcTop, Mesh mesh, Vector2 centerPosLocal)
        {
            try
            {
                this.dc = dc;
                markId = (short)(dcTop + dc.Id);
                core.ReMarkCells(dc.cells, markId);

                ComputeStartPoint(dc.LeftCell, dc.LeftDir, out leftPoint, out startDir, out leftCell, true);
                ComputeStartPoint(dc.RightCell, dc.RightDir, out rightPoint, out endDir, out rightCell, false);
                InitBounds(leftPoint);

                FindFrontEdge();
                CalcCutDistances();
                TriangulateFront();
                AddBackEdge(centerPosLocal);
                TriangulateBack();
                TriangulateSide();

                SetMesh(mesh);
            }
            finally 
            {
                Clear();
            }
        }

        private void InitBounds(Vector2 point)
        {
            minX = maxX = point.x;
            minY = maxY = point.y;
        }
        private void ExtendBounds(Vector2 point)
        {
            minX = Mathf.Min(minX, point.x);
            maxX = Mathf.Max(maxX, point.x);
            minY = Mathf.Min(minY, point.y);
            maxY = Mathf.Max(maxY, point.y);
        }

        private void Clear()
        {
            points.Clear();
            triangles.Clear();
            pointNodes.Clear();
            workNodes.Clear();
        }

        private void ComputeStartPoint(Vector2Int cell, Vector2 dir, out Vector2 point, out Vector2Int outDir, out Vector2Int outCell, bool isLeft)
        {
            var pivot = VCore.CellPivot(cell);
            Vector2 normal = VCore.TurnLeft(dir) * (isLeft ? 1 : -1);

            point = pivot + pointsDesc[0].point;
            float x = Vector2.Dot(normal, point);
            int outIndex = 0;
            TestBetterPoint(pivot, 1, normal, ref x, ref point, ref outIndex);
            TestBetterPoint(pivot, 2, normal, ref x, ref point, ref outIndex);
            TestBetterPoint(pivot, 3, normal, ref x, ref point, ref outIndex);

            outDir = isLeft ? pointsDesc[outIndex].lOut : pointsDesc[outIndex].rOut;
            var nextCellDir = isLeft ? TurnRight(outDir) : TurnLeft(outDir);
          

            if (IsColored(cell + nextCellDir))
            {
                // kdyz je startovni bunka zanorena, potrebuju se vyskrabat na kraj
                outCell = cell + nextCellDir;
                outDir = nextCellDir;
                point -= (Vector2)nextCellDir * 0.1f;

                nextCellDir = isLeft ? TurnRight(outDir) : TurnLeft(outDir);
                while (IsColored(outCell + nextCellDir))
                {
                    outCell += nextCellDir;
                    point += (Vector2)nextCellDir * 0.5f;
                }
            }
            else
            {
                outCell = cell;
            }
        }

        private void TestBetterPoint(Vector2 pivot, int index, Vector2 normal, ref float x, ref Vector2 result, ref int outIndex)
        {
            Vector2 candidate = pivot + pointsDesc[index].point;
            float x2 = Vector2.Dot(normal, candidate);
            if (x2 > x)
            {
                x = x2;
                result = candidate;
                outIndex = index;
            }
        }


        private void FindFrontEdge()
        {
            AddPoint(leftPoint);
            leftPNode = pointNodes.Ptr;

            Vector2 point = leftPoint;
            Vector2Int cell = leftCell;
            Vector2Int dir = startDir;
            Vector2Int endDir = -this.endDir;
            point += 0.05f * (Vector2)dir;
            int counter = 0;

            while (cell != rightCell || dir != endDir)
            {
                counter++;
                if (counter > dc.cells.Count * 4)
                    throw new CyclusException(dc.Id);

                var crossCell = cell + CrossPos(dir);
                if (IsColored(crossCell))
                {
                    cell = crossCell;
                    point += 0.45f * (Vector2)dir;
                    dir = TurnRight(dir);
                    AddPoint(point);
                    point -= 0.05f * (Vector2)dir;
                }
                else
                {
                    var nextCell = cell + dir;
                    if (IsColored(nextCell))
                    {
                        point += 0.5f * (Vector2)dir;
                        cell = nextCell;
                    }
                    else
                    {
                        point += 0.55f * (Vector2)dir;
                        dir = TurnLeft(dir);
                        AddPointWithWork(point);
                        point += 0.05f * (Vector2)dir;
                    }
                }
            }

            Debug.Assert(point + 0.55f * (Vector2)dir == rightPoint, "nedojel jsem do rightPoint");

            rightPIndex = points.Count;
            AddPoint(rightPoint);
            rightPNode = pointNodes.Ptr;
        }

        private void AddPoint(Vector2 point)
        {
            pointNodes.InsertAfter() = new PointDesc(point, points.Count);
            points.Add(point);
            ExtendBounds(point);
        }

        private void AddPointWithWork(Vector2 point)
        {
            ref var node = ref pointNodes.InsertAfter();
            ref var work = ref workNodes.InsertAfter();
            node = new PointDesc(point, points.Count, workNodes.Ptr);
            work = new Work(pointNodes.Ptr);
            points.Add(point);
            ExtendBounds(point);
        }

        private void CalcCutDistances()
        {
            workNodes.ResetPtr();
            while (workNodes.MoveNext())
            {
                ref var node = ref workNodes.Get();
                var ptr = node.pointNode;
                var lp = pointNodes.Get(pointNodes.MovePrev(ptr)).point;
                var rp = pointNodes.Get(pointNodes.MoveNext(ptr)).point;
                var dist = (lp - rp).sqrMagnitude;
                node.sqrDist = dist;
            }
        }

        private void TriangulateFront()
        {
            while (workNodes.Count > 0)
            {
                float dist = PickVertex();
                float distLimit = dist;

                for ( ; ; )
                {
                    ref var ln = ref pointNodes.GetPrev();
                    ushort cIndex = pointNodes.Get().index;
                    ref var rn = ref pointNodes.GetNext();

                    if (dist > 0)
                    {
                        triangles.Add(ln.index);
                        triangles.Add(rn.index);
                        triangles.Add(cIndex);
                    }

                    pointNodes.Remove();
                    
                    var rp = pointNodes.Ptr;
                    var lp = pointNodes.MovePrev(rp);
                    var rrp = pointNodes.MoveNext(rp);
                    var llp = pointNodes.MovePrev(lp);
                    float lDist = -1;
                    float rDist = -1;

                    if (rrp != 0)
                    {
                        rDist = FixVertex(ref ln, ref rn, ref pointNodes.Get(rrp), rp);
                    }
                    
                    if (llp != 0)
                    {
                        lDist = FixVertex(ref pointNodes.Get(llp), ref ln, ref rn, lp);
                    }

                    if (lDist >= 0 && lDist <= distLimit)
                    {
                        dist = lDist;
                        FastPickVertex(lp);
                    }
                    else if (rDist >= 0 && rDist <= distLimit)
                    {
                        dist = rDist;
                        FastPickVertex(rp);
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }


        private float FixVertex(ref PointDesc n1, ref PointDesc n2, ref PointDesc n3, ushort n2Ptr)
        {
            var dir1 = n1.point - n2.point;
            var dir2 = n3.point - n2.point;
            var cross = VCore.CrossProduct(dir1, dir2);

            if (cross == 0)
            {
                SetWorkNode(ref n2, 0, n2Ptr);
                return 0;
            }
            else if (cross < 0)
            {
                var dist = (n1.point - n3.point).sqrMagnitude;
                SetWorkNode(ref n2, dist, n2Ptr);
                return dist;
            }
            else
            {
                if (n2.workNode > 0)
                {
                    workNodes.Remove(n2.workNode);
                    n2.workNode = 0;
                }
                return -1;
            }
        }

        private void SetWorkNode(ref PointDesc node, float dist, ushort n2Ptr)
        {
            if (node.workNode == 0)
            {
                ref var wn = ref workNodes.InsertAfter();
                wn.pointNode = n2Ptr;
                wn.sqrDist = dist;
                node.workNode = workNodes.Ptr;
            }
            else
            {
                workNodes.Get(node.workNode).sqrDist = dist;
            }
        }

        private float PickVertex()
        {
            workNodes.ResetPtr();
            float dist = float.MaxValue;
            ushort ptr = 0;
            while (workNodes.MoveNext())
            {
                float dist2 = workNodes.Get().sqrDist;
                if (dist2 < dist)
                {
                    dist = dist2;
                    ptr = workNodes.Ptr;
                }
            }

            pointNodes.Ptr = workNodes.Get(ptr).pointNode;
            workNodes.Remove(ptr);
            return dist;
        }

        private void FastPickVertex(ushort ptr)
        {
            pointNodes.Ptr = ptr;
            workNodes.Remove(pointNodes.Get().workNode);
        }

        private void AddBackEdge(Vector2 centerPosLocal)
        {
            var leftDir = dc.LeftDir.normalized;
            var rightDir = dc.RightDir.normalized;
            var leftLen = VCore.ShadowRadius - Vector2.Dot(leftPoint - centerPosLocal, leftDir);
            var rightLen = VCore.ShadowRadius - Vector2.Dot(rightPoint - centerPosLocal, rightDir);

            pointNodes.Ptr = leftPNode;
            pointNodes.MovePrev();
            AddPoint(leftPoint + leftDir * leftLen);

            pointNodes.Ptr = rightPNode;
            AddPoint(rightPoint + rightDir * rightLen);

            if (Vector2.Dot(leftDir, rightDir) < 0.5f)
            {
                var centerDir = (leftDir + rightDir).normalized;
                AddPoint(centerPosLocal + centerDir * VCore.ShadowRadius);
            }

            pointNodes.MakeLoop();
        }

        private void TriangulateBack()
        {
            ushort ptr = LeftRightTringulate();
            TringulateRest(ptr);
            ptr = pointNodes.Ptr;
            Collapse(ref ptr, true);
        }


        private ushort LeftRightTringulate()
        {
            ushort ptr1 = leftPNode;
            ushort ptr2 = rightPNode;
            var a1 = TestAngle(ptr1);
            var a2 = TestAngle(ptr2);

            while (pointNodes.Count > 3)
            {
                if (a1 < a2 && a1 < 0)
                {
                    Collapse(ref ptr1, moveNext: true);
                    if (ptr1 == ptr2)
                        break;
                    a1 = TestAngle(ptr1);
                }
                else if (a2 < a1 && a2 < 0)
                {
                    Collapse(ref ptr2, moveNext: false);
                    if (ptr1 == ptr2)
                        break;
                    a2 = TestAngle(ptr2);
                }
                else
                {
                    break;
                }
            }

            return ptr1;
        }

        private void TringulateRest(ushort ptr)
        {
            int badCounter = 0;
            while (pointNodes.Count > 3)
            {
                var a = TestAngle(ptr);
                if (a == 0)
                {
                    pointNodes.Remove(ptr);
                    ptr = pointNodes.Ptr;
                    badCounter = 0;
                }
                else if (a < 0)
                {
                    Collapse(ref ptr, moveNext: true);
                    badCounter = 0;
                }
                else
                {
                    ptr = pointNodes.MoveNext(ptr);
                    badCounter++;
                    if (badCounter > pointNodes.Count)
                        throw new InvalidOperationException("Cyklus v TringulateRest");
                }
            }
        }

        private void Collapse(ref ushort ptr, bool moveNext)
        {
            pointNodes.Ptr = ptr;
            triangles.Add(pointNodes.GetPrev().index);
            triangles.Add(pointNodes.GetNext().index);
            triangles.Add(pointNodes.Get().index);
            
            pointNodes.Remove();
            if (!moveNext)
                pointNodes.MovePrev();
            ptr = pointNodes.Ptr;
        }

        private float TestAngle(ushort ptr)
        {
            pointNodes.Ptr = ptr;
            var pointCenter = pointNodes.Get().point;
            var dir1 = pointNodes.GetPrev().point - pointCenter;
            var dir2 = pointNodes.GetNext().point - pointCenter;
            return VCore.CrossProduct(dir1, dir2);

        }

        private void TriangulateSide()
        {
            MakeSide(rightPIndex + 1, 0);
            MakeSide(rightPIndex, rightPIndex + 2);
            for (int i = 0; i < rightPIndex; i++)
            {
                MakeSide(i, i + 1);
            }
        }

        private void MakeSide(int i1, int i2)
        {
            triangles.Add((ushort)i1);
            triangles.Add((ushort)i2);
            triangles.Add((ushort)(i2 + points.Count));
            triangles.Add((ushort)(i2 + points.Count));
            triangles.Add((ushort)(i1 + points.Count));
            triangles.Add((ushort)i1);
        }

        private bool IsColored(Vector2Int pos) => core.Get(pos).darkCaster == markId;
        private static Vector2Int CrossPos(Vector2Int dir) => new Vector2Int(dir.x + dir.y, dir.y - dir.x);
        private static Vector2Int TurnLeft(Vector2Int vec) => new Vector2Int(-vec.y, vec.x);
        private static Vector2Int TurnRight(Vector2Int vec) => new Vector2Int(vec.y, -vec.x);

        private void SetMesh(Mesh mesh)
        {
            Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData meshData = meshDataArray[0];

            int vertexAttributeCount = 1;
            int vertexCount = points.Count * 2;
            int triangleIndexCount = triangles.Count;

            var vertexAttributes = new NativeArray<VertexAttributeDescriptor>(vertexAttributeCount, Allocator.Temp);
            vertexAttributes[0] = new VertexAttributeDescriptor(dimension: 3);
            meshData.SetVertexBufferParams(vertexCount, vertexAttributes);
            vertexAttributes.Dispose();

            NativeArray<Vector3> positions = meshData.GetVertexData<Vector3>();
            for (int i = 0; i < points.Count; i++)
            {
                positions[i] = points[i].AddZ(VCore.ShadowFrontZ);
            }
            for (int i = 0; i < points.Count; i++)
            {
                positions[i + points.Count] = points[i].AddZ(VCore.ShadowBackZ);
            }

            meshData.SetIndexBufferParams(triangleIndexCount, IndexFormat.UInt16);
            NativeArray<ushort> triangleIndices = meshData.GetIndexData<ushort>();
            for (int i = 0; i < triangles.Count; i++)
            {
                triangleIndices[i] = triangles[i];
            }

            meshData.subMeshCount = 1;
            var bounds = new Bounds(
                new Vector3((maxX + minX) / 2, (maxY + minY) / 2, (VCore.ShadowFrontZ + VCore.ShadowBackZ) / 2), 
                new Vector3(maxX - minX, maxY - minY, VCore.ShadowBackZ - VCore.ShadowFrontZ));
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, triangleIndexCount)
            {
                bounds = bounds,
                vertexCount = vertexCount
            }, MeshUpdateFlags.DontRecalculateBounds);

            mesh.bounds = bounds;
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
        }
    }
}
