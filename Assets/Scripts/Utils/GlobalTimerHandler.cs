using Assets.Scripts.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Utils
{
    public class GlobalTimerHandler : MonoBehaviour
    {
        private new int tag;
        private readonly Dictionary<Label, (int objTag, int count)> activeObjects = new Dictionary<Label, (int, int)>();
        private readonly Dictionary<int, (Label liveObj, int objTag)> activeEvents = new Dictionary<int, (Label, int)>();
        private readonly Action<object, int> timerAction;
        public ParamsHandler<Ksid, float> WithKsidFloatParams { get; }

        public GlobalTimerHandler()
        {
            timerAction = OnTimer;
            WithKsidFloatParams = new ParamsHandler<Ksid, float>(this);
        }

        public void Plan(float delta, Action<Label> action, Label liveObj)
        {
            int objTag = StoreTags(liveObj);
            activeEvents.Add(tag, (liveObj, objTag));
            Game.Instance.Timer.Plan(timerAction, delta, action, tag);
        }

        private int StoreTags(Label liveObj)
        {
            tag++;
            if (!activeObjects.TryGetValue(liveObj, out var objInfo))
            {
                objInfo = (tag, 1);
                activeObjects.Add(liveObj, objInfo);
            }
            else
            {
                activeObjects[liveObj] = (objInfo.objTag, objInfo.count + 1);
            }

            return objInfo.objTag;
        }

        public void ObjectDied(Label obj) => activeObjects.Remove(obj);

        private void OnTimer(object obj, int tag)
        {
            activeEvents.Remove(tag, out var evInfo);
            if (CheckTags(evInfo.liveObj, evInfo.objTag))
                ((Action<Label>)obj)(evInfo.liveObj);
        }

        private bool CheckTags(Label liveObj, int objTag)
        {
            if (activeObjects.TryGetValue(liveObj, out var objInfo) && objTag == objInfo.objTag)
            {
                if (objInfo.count <= 1)
                {
                    activeObjects.Remove(liveObj);
                }
                else
                {
                    activeObjects[liveObj] = (objInfo.objTag, objInfo.count - 1);
                }
                return true;
            }
            return false;
        }

        public class ParamsHandler<T1, T2>
        {
            private readonly Dictionary<int, (Label liveObj, int objTag, T1 p1, T2 p2)> activeEvents = new Dictionary<int, (Label, int, T1, T2)>();
            private readonly Action<object, int> timerAction;
            private readonly GlobalTimerHandler parent;

            public ParamsHandler(GlobalTimerHandler parent)
            {
                this.parent = parent;
                timerAction = OnTimer;
            }

            public void Plan(float delta, Action<Label, T1, T2> action, Label liveObj, T1 p1, T2 p2)
            {
                int objTag = parent.StoreTags(liveObj);
                activeEvents.Add(parent.tag, (liveObj, objTag, p1, p2));
                Game.Instance.Timer.Plan(timerAction, delta, action, parent.tag);
            }

            private void OnTimer(object obj, int tag)
            {
                activeEvents.Remove(tag, out var evInfo);
                if (parent.CheckTags(evInfo.liveObj, evInfo.objTag))
                    ((Action<Label, T1, T2>)obj)(evInfo.liveObj, evInfo.p1, evInfo.p2);
            }
        }
    }
}
