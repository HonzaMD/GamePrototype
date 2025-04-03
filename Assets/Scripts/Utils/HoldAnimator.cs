using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Utils
{
    public class HoldAnimator
    {
        private static Stack<HoldAnimator> pool = new();

        private Transform leg;
        private Vector2 originalHoldTarget;
        private AnimationCurve animation;
        private float returnTime;
        private float startTime;
        private float lastValue;
        private float speed;
        private Vector2? returnDir;
        public bool Completed { get; private set; }

        public static HoldAnimator Create(Transform leg, Vector2 holdTarget, AnimationCurve animation, float returnTime, float speed)
        {
            HoldAnimator animator = pool.Count == 0 ? new HoldAnimator() : pool.Pop();
            animator.Init(leg, holdTarget, animation, returnTime, speed);
            return animator;
        }

        private void Init(Transform leg, Vector2 holdTarget, AnimationCurve animation, float returnTime, float speed)
        {
            startTime = Time.time;
            this.leg = leg;
            this.originalHoldTarget = holdTarget;
            this.animation = animation;
            this.returnTime = returnTime;
            this.speed = speed;
            Completed = false;
            lastValue = 0;
            returnDir = null;
        }

        public Vector2 Cancel() 
        { 
            pool.Push(this);
            return originalHoldTarget; 
        }

        public Vector2 Evaluate(Vector2 holdTarget)
        {
            if (Completed)
                return originalHoldTarget;

            float time = (Time.time - startTime) * speed;
            if (time >= 1)
            {
                Completed = true;
                return originalHoldTarget;
            }

            Vector2 dir = time < returnTime ? leg.rotation * Vector2.down : GetReturnDir(holdTarget);

            if (Completed)
                return originalHoldTarget;
            
            float newValue = animation.Evaluate(time);
            float deltaValue = newValue - lastValue;
            lastValue = newValue;

            return holdTarget + dir * deltaValue;
        }

        private Vector2 GetReturnDir(Vector2 holdTarget)
        {
            if (returnDir.HasValue)
                return returnDir.Value;
            var dir = holdTarget - originalHoldTarget;
            if (dir.sqrMagnitude < 0.0001 || Math.Abs(lastValue) < 0.001)
            {
                Completed = true;
                return Vector2.zero;
            }

            returnDir = dir / lastValue;
            return returnDir.Value;
        }
    }
}
