﻿using System;
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
            where T : MonoBehaviour
        {
            for (int f = 0; f < transform.childCount; f++)
            {
                var c = transform.GetChild(f).GetComponent<T>();
                if (c)
                    output.Add(c);
            }
        }

        public static void GetComponentsInLevel1Children<T>(this MonoBehaviour obj, List<T> output)
            where T : MonoBehaviour
            => GetComponentsInLevel1Children(obj.transform, output);

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

        public static void Kill(this Label prototype, Label obj)
            => Game.Instance.Pool.Store(obj, prototype);
    }
}
