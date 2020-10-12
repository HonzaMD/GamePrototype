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
    }
}
