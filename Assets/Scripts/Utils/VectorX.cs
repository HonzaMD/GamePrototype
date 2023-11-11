using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Utils
{
    static class VectorX
    {
        public static Vector2 XY(this Vector3 v) => new Vector2(v.x, v.y);
        public static Vector2 XZ(this Vector3 v) => new Vector2(v.x, v.z);
        public static Vector2 YZ(this Vector3 v) => new Vector2(v.y, v.z);
        public static Vector3 AddZ(this Vector2 v, float z) => new Vector3(v.x, v.y, z);
        public static Vector3 WithZ(this Vector3 v, float z) => new Vector3(v.x, v.y, z);

        public static Vector3 PlusX(this Vector3 v, float x) => new Vector3(v.x + x, v.y, v.z);
        public static Vector3 PlusY(this Vector3 v, float y) => new Vector3(v.x, v.y + y, v.z);

        public static Vector2 Abs(this Vector2 v) => new Vector2(Mathf.Abs(v.x), Mathf.Abs(v.y));
        public static Vector3 Abs(this Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        public static bool IsLessEq(this Vector2 a, Vector2 b) => a.x <= b.x && a.y <= b.y;
        public static bool IsLessEq(this Vector3 a, Vector3 b) => a.x <= b.x && a.y <= b.y && a.z <= b.z;
        public static bool IsYLessXGrEq(this Vector2 a, Vector2 b) => a.x >= b.x && a.y <= b.y;
    }
}
