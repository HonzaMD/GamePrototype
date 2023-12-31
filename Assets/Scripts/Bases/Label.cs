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

    public virtual Rigidbody Rigidbody
    {
        get
        {
            var kl = KillableLabel();
            if (kl.TryGetComponent<Rigidbody>(out var res))
                return res;
            return kl.transform.parent.TryGetComponent(out res) ? res : null;
        }
    }

    public abstract Ksid KsidGet { get; }
    public virtual Vector3 GetClosestPoint(Vector3 position) => GetComponentInChildren<Collider>().ClosestPoint(position);

    public virtual void Cleanup() 
    {
        var connectables = ListPool<IConnectable>.Rent();
        ParentForConnections.GetComponentsInLevel1Children(connectables);
        foreach (var c in connectables)
        {
            if (c.Type != ConnectableType.Off)
                c.Disconnect();
        }
        connectables.Return();

        Game.Instance.GlobalTimerHandler.ObjectDied(this);
    }

    public bool TryGetRbLabel(out RbLabel rbLabel)
    {
        var rb = Rigidbody;
        if (!rb)
        {
            rbLabel = null;
            return false;
        }
        return rb.TryGetComponent(out rbLabel);
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

    /// <summary>
    /// Volat jen z jednorazovych efektu nebo z FixedUpdate
    /// </summary>
    public virtual void ApplyVelocity(Vector3 velocity, float sourceMass, VelocityFlags flags)
    {
        if (velocity.sqrMagnitude * sourceMass * sourceMass < PhysicsConsts.TooSmallSqr)
            return;

        bool dontAffectRb = (flags & VelocityFlags.DontAffectRb) != 0;
        var rb = dontAffectRb ? null : Rigidbody;
        if (rb)
        {
            rb.AddForce(velocity, sourceMass, flags);
        }
        else if (KsidGet.IsChildOf(Ksid.SandLike) && TryGetParentLabel(out var pl) && pl is SandCombiner sandCombiner)
        {
            if (dontAffectRb)
            {
                sandCombiner.ApplyVelocityThroughSandCombiner(velocity, sourceMass, flags);
            }
            else
            {
                CollapseSandByForce(velocity, sourceMass, flags, sandCombiner);
            }
        }
        else if (KsidGet.IsChildOf(Ksid.SpMoving) && this is Placeable p && p.SpNodeIndex != 0)
        {
            Game.Instance.StaticPhysics.ApplyTempForce(p.SpNodeIndex, velocity, sourceMass, flags);
        }
        else if (this is SandCombiner sandCombiner2)
        {
            sandCombiner2.ApplyVelocityThroughSandCombiner(velocity, sourceMass, flags);
        }
        else if (!CanBeKilled)
        {
            KillableLabel().ApplyVelocity(velocity, sourceMass, flags);
        }
    }


    private void CollapseSandByForce(Vector3 velocity, float sourceMass, VelocityFlags flags, SandCombiner sandCombiner)
    {
        float scCenterX = sandCombiner.Pivot.x + 0.25f;
        float toCenterX = scCenterX - Pivot.x;
        var toSand = new Vector2(toCenterX, -0.25f);
        if (Vector2.Dot(toSand, velocity.XY()) < -0.1f)
        {
            sandCombiner.CollapseNow();
            Rigidbody.AddForce(velocity, sourceMass, flags);
        }
        else
        {
            sandCombiner.ApplyVelocityThroughSandCombiner(velocity, sourceMass, flags);
        }
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
    public bool HasActiveRB
    {
        get
        {
            var rb = Rigidbody;
            return (rb && !rb.isKinematic);
        }
    }

    public static bool TryFind(Transform collision, out Label lb)
    {
        while (!collision.TryGetComponent(out lb) && collision.parent != null)
            collision = collision.parent;
        return lb != null;
    }

    public bool HasRB => Rigidbody;
    public Transform LevelGroup => GetComponentInParent<LevelLabel>().transform;
    public bool TryGetParentLabel(out Label label) => transform.TryFindInParents(out label);
    public bool TryGetKillableParentLabel(out Label label)
    {
        var t = transform.parent;
        while (t)
        {
            if (t.TryGetComponent(out label) && label.CanBeKilled)
                return true;
            t = t.parent;
        }
        label = default;
        return false;
    }
    public Label KillableLabel()
    {
        if (CanBeKilled)
        {
            return this;
        }
        else
        {
            TryGetKillableParentLabel(out var killableLabel);
            return killableLabel;
        }
    }
    public bool IsTopLabel => transform.parent.TryGetComponent(out LevelLabel _);
    public Vector2 Pivot => transform.position.XY();
    public int CellZ => transform.position.z < 0.25f ? 0 : 1;

    public abstract bool IsAlive { get; }

    public virtual void Kill()
    { 
        if (CanBeKilled)
        {
            if (!IsAlive)
                throw new InvalidOperationException("Zabijis neco, co nezije!");

            if (TryGetParentLabel(out var pl))
                pl.DetachKilledChild(this);

            var labelsToKill = ListPool<Label>.Rent();
            RecursiveCleanup(labelsToKill);

            foreach (var l in labelsToKill)
                l.KillMe();
            
            labelsToKill.Return();
        }
        else if (TryGetKillableParentLabel(out var pl))
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

    public void DisconnectConnectables(ConnectableType type)
    {
        var connectables = ListPool<IConnectable>.Rent();
        
        DisconnectConnectables(this, type, connectables);

        if (IsGroup)
        {
            var labels = ListPool<Label>.Rent();
            GetComponentsInChildren(labels);
            foreach (var l in labels)
                if (l != this)
                    DisconnectConnectables(l, type, connectables);
            labels.Return();
        }

        connectables.Return();
    }

    private static void DisconnectConnectables(Label label, ConnectableType type, List<IConnectable> connectables)
    {
        label.ParentForConnections.GetComponentsInLevel1Children(connectables);
        foreach (var c in connectables)
        {
            if (c.Type == type)
                c.Disconnect();
        }
        connectables.Clear();
    }
}