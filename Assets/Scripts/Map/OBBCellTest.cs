using UnityEngine;

namespace Assets.Scripts.Map
{
    public readonly struct OBBCellTest
    {
        private readonly Vector2 c0, c1, c2, c3;
        private readonly Vector2 axis0, axis1;
        private readonly float obbMinX, obbMaxX, obbMinY, obbMaxY;
        private readonly float obbMin0, obbMax0, obbMin1, obbMax1;
        private readonly float cellW, cellH;

        public OBBCellTest(Vector2 c0, Vector2 c1, Vector2 c2, Vector2 c3, Vector2 cellSize)
        {
            this.c0 = c0; this.c1 = c1; this.c2 = c2; this.c3 = c3;
            cellW = cellSize.x; cellH = cellSize.y;

            var edge0 = c1 - c0;
            axis0 = new Vector2(-edge0.y, edge0.x);
            var edge1 = c3 - c0;
            axis1 = new Vector2(-edge1.y, edge1.x);

            obbMinX = Min4(c0.x, c1.x, c2.x, c3.x);
            obbMaxX = Max4(c0.x, c1.x, c2.x, c3.x);
            obbMinY = Min4(c0.y, c1.y, c2.y, c3.y);
            obbMaxY = Max4(c0.y, c1.y, c2.y, c3.y);

            ProjectCorners(axis0, c0, c1, c2, c3, out obbMin0, out obbMax0);
            ProjectCorners(axis1, c0, c1, c2, c3, out obbMin1, out obbMax1);
        }

        public OBBCellTest WithCellSize(Vector2 cellSize) => new(c0, c1, c2, c3, cellSize);

        public bool SameCorners(in OBBCellTest other) => c0 == other.c0 && c1 == other.c1 && c2 == other.c2 && c3 == other.c3;

        public bool Intersects(float cellWorldX, float cellWorldY)
        {
            float cMaxX = cellWorldX + cellW;
            float cMaxY = cellWorldY + cellH;

            if (obbMaxX <= cellWorldX || obbMinX >= cMaxX) return false;
            if (obbMaxY <= cellWorldY || obbMinY >= cMaxY) return false;

            ProjectAABB(axis0, cellWorldX, cellWorldY, cMaxX, cMaxY, out var cMin, out var cMax);
            if (obbMax0 <= cMin || obbMin0 >= cMax) return false;

            ProjectAABB(axis1, cellWorldX, cellWorldY, cMaxX, cMaxY, out cMin, out cMax);
            if (obbMax1 <= cMin || obbMin1 >= cMax) return false;

            return true;
        }

        static void ProjectCorners(Vector2 axis, Vector2 c0, Vector2 c1, Vector2 c2, Vector2 c3, out float min, out float max)
        {
            float d0 = Vector2.Dot(axis, c0);
            float d1 = Vector2.Dot(axis, c1);
            float d2 = Vector2.Dot(axis, c2);
            float d3 = Vector2.Dot(axis, c3);
            min = Min4(d0, d1, d2, d3);
            max = Max4(d0, d1, d2, d3);
        }

        static void ProjectAABB(Vector2 axis, float minX, float minY, float maxX, float maxY, out float min, out float max)
        {
            float d0 = axis.x * minX + axis.y * minY;
            float d1 = axis.x * maxX + axis.y * minY;
            float d2 = axis.x * minX + axis.y * maxY;
            float d3 = axis.x * maxX + axis.y * maxY;
            min = Min4(d0, d1, d2, d3);
            max = Max4(d0, d1, d2, d3);
        }

        static float Min4(float a, float b, float c, float d)
            => Mathf.Min(a, Mathf.Min(b, Mathf.Min(c, d)));
        static float Max4(float a, float b, float c, float d)
            => Mathf.Max(a, Mathf.Max(b, Mathf.Max(c, d)));
    }
}
