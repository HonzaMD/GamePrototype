using Assets.Scripts;
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
    public virtual Rigidbody Rigidbody => GetComponent<Rigidbody>();
    public virtual Transform ParentForConnections => transform;
    public virtual Ksid Ksid => PlaceableC.Ksid;
    public virtual Vector3 GetClosestPoint(Vector3 position) => GetComponentInChildren<Collider>().ClosestPoint(position);

    private static readonly List<Collider> colliders1 = new List<Collider>();
    private static readonly List<Collider> colliders2 = new List<Collider>();

    public virtual void GetColliders(List<Collider> result)
    {
        result.Clear();
        GetComponentsInChildren<Collider>(result);
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

    public static Transform Find(Transform collision, out Label lb)
    {
        while (!collision.TryGetComponent<Label>(out lb) && collision.parent != null)
            collision = collision.parent;
        return lb == null ? collision : lb.ParentForConnections;
    }
}
