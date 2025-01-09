using Assets.Scripts.Bases;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Utils
{
    public static class Extensions
    {
        public static T GetComponentInFirstChildren<T>(this Transform transform)
            where T : class 
            => transform.childCount > 0 ? transform.GetChild(0).GetComponentInChildren<T>() : null;

        public static T GetComponentInFirstChildren<T>(this MonoBehaviour obj)
            where T : class
            => obj.transform.GetComponentInFirstChildren<T>();

        public static void GetComponentsInLevel1Children<T>(this Transform transform, List<T> output)
        {
            for (int f = 0; f < transform.childCount; f++)
            {
                if (transform.GetChild(f).TryGetComponent(out T c))
                    output.Add(c);
            }
        }

        public static void GetComponentsInLevel1Children<T>(this MonoBehaviour obj, List<T> output)
            => GetComponentsInLevel1Children(obj.transform, output);

        public static bool TryFindInParents<T>(this Transform transform, out T obj)
        {
            var t = transform.parent;
            while (t)
            {
                if (t.TryGetComponent(out obj))
                    return true;
                t = t.parent;
            }
            obj = default;
            return false;
        }

        public static T ToRealNull<T>(this T obj)
            where T : UnityEngine.Object
        {
            if (!obj)
                return null;
            return obj;
        }

        public static T Create<T>(this T prototype, Transform parent, Vector3 position, Map.Map map)
            where T : Label
        {
            var obj = Game.Instance.Pool.Get(prototype, parent, position);
            obj.Init(map); 
            return obj;
        }

        public static T CreateWithotInit<T>(this T prototype, Transform parent)
            where T : Label 
            => Game.Instance.Pool.Get(prototype, parent);

        public static T CreateWithotInit<T>(this T prototype, Transform parent, Vector3 position)
            where T : Label
            => Game.Instance.Pool.Get(prototype, parent, position);

        public static T CreateCL<T>(this T prototype, Transform parent, Vector3 position)
            where T : ConnectableLabel
            => Game.Instance.ConnectablePool.Get(prototype, parent, position);

        public static T CreateCL<T>(this T prototype, Transform parent)
            where T : ConnectableLabel
            => Game.Instance.ConnectablePool.Get(prototype, parent);

        
        public static bool Touches(this Collider collider1, Collider collider2, float margin)
        {
            if (collider1.enabled && collider2.enabled)
            {
                Bounds b1 = collider1.bounds;
                b1.Expand(margin * 2);
                if (b1.Intersects(collider2.bounds))
                {
                    float marginSq = margin * margin;
                    Vector3 center1 = collider1.bounds.center;
                    Vector3 center2 = collider2.bounds.center;

                    var p1 = collider1.ClosestPoint(center2);
                    if ((p1 - center2).sqrMagnitude <= marginSq)
                        return true;
                    var p2 = collider2.ClosestPoint(p1);
                    if ((p1 - p2).sqrMagnitude <= marginSq)
                        return true;

                    p1 = collider2.ClosestPoint(center1);
                    if ((p1 - center1).sqrMagnitude <= marginSq)
                        return true;
                    p2 = collider1.ClosestPoint(p1);
                    if ((p1 - p2).sqrMagnitude <= marginSq)
                        return true;

                    Vector3 intersec = Vector3.Cross(p1 - center1, p1 - p2);
                    if (intersec.sqrMagnitude <= 0.00001f)
                        return false;
                    Vector3 lin1 = Vector3.Cross(intersec, p1 - center1);

                    float koef = Vector3.Dot(p1 - p2, lin1);
                    if (Mathf.Abs(koef) < 0.00001f)
                        return false;
                    Vector3 p3 = p1 - lin1 * (p1 - p2).sqrMagnitude / koef;

                    p1 = collider2.ClosestPoint(p3);
                    p2 = collider1.ClosestPoint(p1);
                    if ((p1 - p2).sqrMagnitude <= marginSq)
                        return true;
                }
            }

            return false;
        }


        public static void Cleanup(this Rigidbody rigidbody)
        {
            if (!rigidbody.isKinematic)
            {
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }
        }

        public static void AddForce(this Rigidbody body, Vector3 velocity, float sourceMass, VelocityFlags flags)
        {
            float koef = sourceMass / body.mass;
            if (koef > 1)
            {
                koef = (flags & VelocityFlags.LimitVelocity) != 0 ? 1 : Mathf.Log(koef) * 0.5f + 1;
            } 
            else
            {
                koef = - 0.5f * koef * koef + 1.5f * koef;
            }

            if ((flags & VelocityFlags.IsImpact) != 0)
                koef *= PhysicsConsts.ImpactDump;

            body.AddForce(velocity * koef, ForceMode.VelocityChange);
        }
    }
}
