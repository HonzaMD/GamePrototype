using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Utils
{
    public class ObjectPool : MonoBehaviour
    {
        private readonly Dictionary<Label, Stack<Label>> cache = new Dictionary<Label, Stack<Label>>();

        public void Store(Label obj, Label prototype)
        {
            obj.Cleanup();
            obj.gameObject.SetActive(false);
            obj.transform.parent = transform;

            Stack<Label> stack = GetStack(prototype);
            stack.Push(obj);
        }

        private Stack<Label> GetStack(Label prototype)
        {
            if (!cache.TryGetValue(prototype, out var stack))
            {
                stack = new Stack<Label>();
                cache.Add(prototype, stack);
            }
            return stack;
        }

        public T Get<T>(T prototype, Transform parent, Vector3 localPosition)
            where T : Label
        {
            Stack<Label> stack = GetStack(prototype);
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
    }
}
