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

    public abstract class ObjectPool<TLabel> : MonoBehaviour
        where TLabel : MonoBehaviour
    {
        private readonly Dictionary<TLabel, Queue<(TLabel Obj, uint Age)>> cache = new Dictionary<TLabel, Queue<(TLabel, uint)>>();
        private uint counter;

        public void Store(TLabel obj, TLabel prototype)
        {
            obj.gameObject.SetActive(false);
            obj.transform.parent = transform;

            var stack = GetStack(prototype);
            stack.Enqueue((obj, counter));
        }

        public void UpdateAgeAtPhysicsUpdate() => counter++;

        private Queue<(TLabel Obj, uint Age)> GetStack(TLabel prototype)
        {
            if (!cache.TryGetValue(prototype, out var stack))
            {
                stack = new Queue<(TLabel, uint)>();
                cache.Add(prototype, stack);
            }
            return stack;
        }

        private bool HasItem(Queue<(TLabel Obj, uint Age)> stack)
        {
            return stack.Count > 0 && stack.Peek().Age + 2 < counter;
        }

        public T Get<T>(T prototype, Transform parent, Vector3 localPosition)
            where T : TLabel
        {
            var stack = GetStack(prototype);
            T ret;
            if (HasItem(stack))
            {
                ret = (T)stack.Dequeue().Obj;
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
            var stack = GetStack(prototype);
            T ret;
            if (HasItem(stack))
            {
                ret = (T)stack.Dequeue().Obj;
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
