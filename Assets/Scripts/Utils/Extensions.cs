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

        public static T Create<T>(this T prototype, Transform parent, Vector3 localPosition)
            where T : Label
            => Game.Instance.Pool.Get(prototype, parent, localPosition);

        public static T Create<T>(this T prototype, Transform parent)
            where T : Label
            => Game.Instance.Pool.Get(prototype, parent);

        public static T CreateCL<T>(this T prototype, Transform parent, Vector3 localPosition)
            where T : ConnectableLabel
            => Game.Instance.ConnectablePool.Get(prototype, parent, localPosition);

        public static T CreateCL<T>(this T prototype, Transform parent)
            where T : ConnectableLabel
            => Game.Instance.ConnectablePool.Get(prototype, parent);
    }
}
