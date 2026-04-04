using Assets.Scripts;
using Assets.Scripts.Bases;
using UnityEngine;

public class ContactDamageTrigger : MonoBehaviour, IFixedUpdateOnce, IHasCleanup
{
    public Trigger ChildTrigger;
    private Placeable placeable;
    private bool scheduled;
    public int ActiveTag { get; set; }

    void Awake()
    {
        placeable = GetComponent<Placeable>();
        ChildTrigger.NewObjectsEvent += OnObjectsChanged;
    }

    public void Cleanup(bool goesToInventory)
    {
        ActiveTag++;
        scheduled = false;
    }

    private void OnObjectsChanged(Trigger t)
    {
        ScheduleIfNeeded();
    }

    private void ScheduleIfNeeded()
    {
        if (!scheduled && ChildTrigger.ActiveObjects.Count > 0)
        {
            scheduled = true;
            Game.Instance.ScheduleFixedUpdate(this);
        }
    }

    public void DoFixedUpdate()
    {
        scheduled = false;
        foreach (var kvp in ChildTrigger.ActiveObjects)
        {
            var target = kvp.Key;
            if (target.IsAlive)
                StaticBehaviour.ApplyContactDamage(placeable, target, target.Center3D);
        }
        ScheduleIfNeeded();
    }
}
