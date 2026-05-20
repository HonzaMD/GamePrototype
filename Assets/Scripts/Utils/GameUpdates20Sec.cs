using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace Assets.Scripts.Utils
{
    internal class GameUpdates20Sec : IActiveObject
    {
        public bool PendingRemove { get; set; }

        private const float MinDelta = 15;
        private const float Delta = 20;
        private const float MaxDelta = 25;
        
        private readonly HashSet<IActiveObject20Sec> activeObjects = new();
        private readonly Queue<(IActiveObject20Sec Obj, float Time)> workList = new();
        private float topTime;


        public void Activate(IActiveObject20Sec activeObject)
        {
            if (activeObjects.Add(activeObject))
            {
                var now = Time.time;
                topTime = Mathf.Max(now, topTime);
                PlanEvent(activeObject, now);
            }
        }

        private void PlanEvent(IActiveObject20Sec activeObject, float now)
        {
            float nextTime = Mathf.Min(now + MaxDelta, topTime + Delta / (activeObjects.Count + Delta * 20));
            Assert.IsTrue(nextTime >= topTime);
            topTime = nextTime;
            workList.Enqueue((activeObject, nextTime));
        }

        public void Deactivate(IActiveObject20Sec activeObject) => activeObjects.Remove(activeObject);


        public void GameUpdate()
        {
            var now = Time.time;
            topTime = Mathf.Max(now + MinDelta, topTime);
            while (workList.Count > 0 && workList.Peek().Time <= now)
            {
                var pair = workList.Dequeue();
                if (activeObjects.Contains(pair.Obj))
                {
                    pair.Obj.GameUpdate20Sec();
                    PlanEvent(pair.Obj, now);
                }
            }
        }

        public void GameFixedUpdate()
        {
        }
    }
}
