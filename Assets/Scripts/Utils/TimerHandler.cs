using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Utils
{
    public interface ISimpleTimerConsumer
    {
        int ActiveTag { get; set; }
        void OnTimer();
    }

    internal static class TimerHandler
    {
        private static readonly Action<object, int> timerAction = OnTimer;
        private static void OnTimer(object o, int tag)
        {
            var obj = (ISimpleTimerConsumer)o;
            if (obj.ActiveTag == tag)
                obj.OnTimer();
        }

        public static void Plan(this ISimpleTimerConsumer obj, float delta)
        {
            obj.ActiveTag++;
            Game.Instance.Timer.Plan(timerAction, delta, obj, obj.ActiveTag);
        }
    }
}
