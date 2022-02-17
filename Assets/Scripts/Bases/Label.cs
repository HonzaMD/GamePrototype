using Assets.Scripts;
using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Label : MonoBehaviour
{
    public abstract Placeable PlaceableC { get; }
    public virtual Transform ParentForConnections => transform;
    public virtual bool CanBeKilled => true;
    public virtual bool IsGroup => false;
    public abstract Label Prototype { get; }

    public virtual Rigidbody Rigidbody => GetComponent<Rigidbody>().ToRealNull() ?? transform.parent.GetComponent<Rigidbody>();
    public abstract Ksid KsidGet { get; }
    public virtual Vector3 GetClosestPoint(Vector3 position) => GetComponentInChildren<Collider>().ClosestPoint(position);

    public virtual void Cleanup() 
    {
        var connectables = ListPool<IConnectable>.Rent();
        ParentForConnections.GetComponentsInLevel1Children(connectables);
        foreach (var c in connectables)
            c.Disconnect();
        connectables.Return();

        Game.Instance.GlobalTimerHandler.ObjectDied(this);
    }


    private static readonly List<Collider> colliders1 = new List<Collider>();
    private static readonly List<Collider> colliders2 = new List<Collider>();

    public virtual void GetColliders(List<Collider> result)
    {
        result.Clear();
        GetComponentsInChildren(result);
    }
    public List<Collider> GetCollidersBuff1()
    {
        GetColliders(colliders1);
        return colliders1;
    }
    public List<Collider> GetCollidersBuff2()
    {
        GetColliders(colliders2);
        return colliders2;
    }

    public virtual void ApplyVelocity(Vector3 velocity)
    {
        var rb = Rigidbody;
        if (rb)
            rb.AddForce(velocity, ForceMode.VelocityChange);
    }

    public virtual Vector3 Velocity
    {
        get
        {
            var rb = Rigidbody;
            if (rb)
                return rb.velocity;
            return Vector3.zero;
        }
    }

    public Vector3 AngularVelocity
    {
        get
        {
            var rb = Rigidbody;
            if (rb)
                return rb.angularVelocity;
            return Vector3.zero;
        }
    }

    public bool IsSpeedy => Velocity.sqrMagnitude > 0.02f/* || AngularVelocity.sqrMagnitude > 0.05f*/;
    public bool IsNonMoving => Velocity.sqrMagnitude < 0.01f/* && AngularVelocity.sqrMagnitude < 0.03f*/;

    public static bool TryFind(Transform collision, out Label lb)
    {
        while (!collision.TryGetComponent(out lb) && collision.parent != null)
            collision = collision.parent;
        return lb != null;
    }

    public bool HasRB => Rigidbody;
    public Transform LevelGroup => GetComponentInParent<LevelLabel>().transform;
    public bool TryGetParentLabel(out Label label) => transform.TryFindInParents(out label);
    public bool IsTopLabel => transform.parent.TryGetComponent(out LevelLabel _);
    public Vector2 Pivot => transform.position.XY();
    public int CellZ => transform.position.z < 0.25f ? 0 : 1;


    public virtual void Kill()
    { 
        if (CanBeKilled)
        {
            if (TryGetParentLabel(out var pl))
                pl.DetachKilledChild(this);

            var labelsToKill = ListPool<Label>.Rent();
            RecursiveCleanup(labelsToKill);

            foreach (var l in labelsToKill)
                l.KillMe();
            
            labelsToKill.Return();
        }
        else if (TryGetParentLabel(out var pl))
        {
            pl.Kill();
        }
    }

    private void RecursiveCleanup(List<Label> labelsToKill)
    {
        Cleanup();
        labelsToKill.Add(this);
        if (!IsGroup)
            return;

        for (int f = transform.childCount - 1; f >= 0; f--)
        {
            RecursiveCleanup(transform.GetChild(f), LevelGroup, labelsToKill);
        }
    }

    private static void RecursiveCleanup(Transform t, Transform levelGroup, List<Label> labelsToKill)
    {
        if (t.TryGetComponent(out Label l))
        {
            l.Cleanup();
            if (l.CanBeKilled)
            {
                l.transform.SetParent(levelGroup, true);
                labelsToKill.Add(l);
            }
            if (!l.IsGroup)
                return;
        }

        for (int f = t.childCount - 1; f >= 0; f--)
        {
            RecursiveCleanup(t.GetChild(f), levelGroup, labelsToKill);
        }
    }

    protected virtual void KillMe()
    {
        var prototype = Prototype;
        if (prototype)
        {
            Game.Instance.Pool.Store(this, prototype);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public virtual void DetachKilledChild(Label child)
    {
        child.transform.SetParent(LevelGroup, true);

        if (IsGroup)
        {
            var labels = ListPool<Label>.Rent();
            GetComponentsInChildren(labels);
            if (labels.Count <= 1)
                Kill();
            labels.Return();
        }
    }
}
