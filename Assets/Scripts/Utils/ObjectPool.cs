using Assets.Scripts.Bases;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Utils
{
    public class ObjectPool : ObjectPool<Label>
    { }

    public class ObjectPool<TLabel> : MonoBehaviour
        where TLabel : MonoBehaviour
    {
        private readonly Dictionary<TLabel, Stack<TLabel>> cache = new Dictionary<TLabel, Stack<TLabel>>();       

        public void Store(TLabel obj, TLabel prototype)
        {
            obj.gameObject.SetActive(false);
            obj.transform.parent = transform;

            Stack<TLabel> stack = GetStack(prototype);
            stack.Push(obj);
        }

        private Stack<TLabel> GetStack(TLabel prototype)
        {
            if (!cache.TryGetValue(prototype, out var stack))
            {
                stack = new Stack<TLabel>();
                cache.Add(prototype, stack);
            }
            return stack;
        }

        public T Get<T>(T prototype, Transform parent, Vector3 localPosition)
            where T : TLabel
        {
            Stack<TLabel> stack = GetStack(prototype);
            T ret;
            if (stack.Count > 0)
            {
                ret = (T)stack.Pop();
                ret.transform.parent = parent;
                ret.transform.localPosition = localPosition;
                ret.gameObject.SetActive(true);
            }
            else
            {
                ret = Instantiate(prototype, parent);
                ret.transform.localPosition = localPosition;
            }
            return ret;
        }

        public T Get<T>(T prototype, Transform parent)
            where T : TLabel
        {
            Stack<TLabel> stack = GetStack(prototype);
            T ret;
            if (stack.Count > 0)
            {
                ret = (T)stack.Pop();
                ret.transform.parent = parent;
                ret.gameObject.SetActive(true);
            }
            else
            {
                ret = Instantiate(prototype, parent);
            }
            return ret;
        }
    }
}
