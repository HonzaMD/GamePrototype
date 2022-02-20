using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Utils
{
    public class Timer : MonoBehaviour
    {
        private readonly BinaryHeap<Event> heap = new BinaryHeap<Event>();

        private struct Event : IComparable<Event>
        {
            public Action<object, int> Action;
            public float Time;
            public object Param;
            public int Token;

            public int CompareTo(Event other)
            {
                return (int)Mathf.Sign(Time - other.Time);
            }
        }

        public void Plan(Action<object, int> action, float timeDelta, object param, int token)
        {
            heap.Add(new Event() { Action = action, Time = Time.time + timeDelta, Param = param, Token = token });
        }

        public void GameUpdate()
        {
            var time = Time.time;
            while (heap.Count > 0 && heap.Peek().Time <= time)
            {
                try
                {
                    var ev = heap.Remove();
                    ev.Action(ev.Param, ev.Token);
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex.ToString());
                }
            }
        }
    }
}
