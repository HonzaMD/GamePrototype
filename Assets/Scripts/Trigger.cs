using Assets.Scripts.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts
{
    public class Trigger : Placeable
    {
        public Ksid Targets;
        public readonly Dictionary<Placeable, int> ActiveObjects = new Dictionary<Placeable, int>();
        public readonly List<Placeable> NewOrRemovedObjects = new List<Placeable>();
        private bool registeredForUpdate;

        public bool TriggerOn => NewOrRemovedObjects.Count == ActiveObjects.Count;
        public bool TriggerOff => ActiveObjects.Count == 0;

        public event Action<Trigger> TriggerOnEvent;
        public event Action<Trigger> NewObjectsEvent;
        public event Action<Trigger> ObjectsRemovedEvent;
        public event Action<Trigger> TriggerOffEvent;

        public Trigger()
        {
            CellBlocking = CellBlocking.Trigger;
        }

        public override Ksid TriggerTargets => Targets;

        public override void AddTarget(Placeable p)
        {
            if (!ActiveObjects.TryGetValue(p, out int c))
                NewOrRemovedObjects.Add(p);
            ActiveObjects[p] = c + 1;
            RegisterForUpdate();
        }


        public override void RemoveTarget(Placeable p)
        {
            ActiveObjects[p]--;
            RegisterForUpdate();
        }

        private void RegisterForUpdate()
        {
            if (!registeredForUpdate)
            {
                registeredForUpdate = true;
                Game.Instance.RegisterTrigger(this);
            }
        }

        public void TriggerUpdate()
        {
            registeredForUpdate = false;

            if (NewOrRemovedObjects.Count > 0)
            {
                if (TriggerOn)
                    TriggerOnEvent?.Invoke(this);
                NewObjectsEvent?.Invoke(this);
                NewOrRemovedObjects.Clear();
            }

            foreach (var p in ActiveObjects)
            {
                if (p.Value == 0)
                    NewOrRemovedObjects.Add(p.Key);
            }
            if (NewOrRemovedObjects.Count > 0)
            {
                foreach (var p in NewOrRemovedObjects)
                    ActiveObjects.Remove(p);
                ObjectsRemovedEvent?.Invoke(this);
                if (TriggerOff)
                    TriggerOffEvent?.Invoke(this);
                NewOrRemovedObjects.Clear();
            }
        }
    }
}
