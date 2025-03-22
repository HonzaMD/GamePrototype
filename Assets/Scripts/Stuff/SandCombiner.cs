using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SandCombiner : Placeable, ISimpleTimerConsumer 
{
    [HideInInspector]
    public int L1;
    [HideInInspector]
    public int L4;

    public Connectable MassTransferer;
    private float mass;
    private Placeable massTarget;

    private int collapsingToken;

    public bool IsFullCell => (SubCellFlags & SubCellFlags.FullEx) != 0;
    public bool Collapsing => (collapsingToken & 1) != 0;

    int ISimpleTimerConsumer.ActiveTag { get => collapsingToken; set => collapsingToken = value; }

    public override float GetMass() => mass;

    private void Start()
    {
        MassTransferer.Init(DisconnectMassTarget);
    }

    public override void Init(Map map) {}

    public void Init(int l1, int l4, bool isFullCell, IEnumerable<Placeable> children, Map map)
    {
        L1 = l1;
        L4 = l4;
        mass = 0;

        SubCellFlags = isFullCell ? SubCellFlags.Full | SubCellFlags.Sand : SubCellFlags.Part | SubCellFlags.Sand;

        if (!isFullCell)
            AdjustSize();

        PlaceToMap(map, false);

        foreach (var p in children)
        {
            if (p)
            {
                mass += p.Rigidbody.mass;
                p.DetachRigidBody(true, false);
                p.transform.SetParent(transform, true);
            }
        }

        TryTransferMass(map);
    }

    private void TryTransferMass(Map map)
    {
        var mt = TryFindMassTarget(map);
        if (mt && mt.SpNodeIndex != 0 && mt.Ksid.IsChildOf(Ksid.SpMoving))
        {
            Game.Instance.StaticPhysics.ApplyForce(mt.SpNodeIndex, Vector2.down * mass);
            massTarget = mt;
            MassTransferer.ConnectTo(mt, ConnectableType.MassTransfer);
        }
    }

    private Placeable TryFindMassTarget(Map map)
    {
        int tag = 0;
        var placeables = ListPool<Placeable>.Rent();
        var myCell = map.WorldToCell(Pivot);
        map.Get(placeables, myCell, Ksid.SpNodeOrSandCombiner, ref tag);
        map.Get(placeables, myCell + Vector2Int.down, Ksid.SpNodeOrSandCombiner, ref tag);

        Placeable mtCandidate = null;
        float mtDistance = float.MaxValue;
        var center = Center3D;

        foreach (var p in placeables)
        {
            if (p != this && center.y >= p.Center.y && (p.SpNodeIndex != 0 || p is SandCombiner || p.Ksid.IsChildOfOrEq(Ksid.SpFixed)))
            {
                var dist = (p.GetClosestPoint(center) - center).sqrMagnitude;
                if (dist < mtDistance)
                {
                    mtDistance = dist;
                    mtCandidate = p;
                }
            }
        }

        placeables.Return();

        if (mtCandidate is SandCombiner sc)
            mtCandidate = sc.massTarget;

        return mtCandidate;
    }

    private Transform DisconnectMassTarget()
    {
        if (massTarget)
        {
            if (massTarget.SpNodeIndex != 0)
                Game.Instance.StaticPhysics.ApplyForce(massTarget.SpNodeIndex, Vector2.up * mass);
            massTarget = null;
        }
        return transform;
    }


    private void AdjustSize()
    {
        var size = Math.Min(L1, L4) * (0.5f / 4f);
        var ubytek = 0.5f - size;
        Size.y = size;
        transform.localPosition += Vector3.up * ubytek;
        var collider = GetComponent<BoxCollider>();
        collider.center = new Vector3(collider.center.x, collider.center.y - ubytek / 2, collider.center.z);
        collider.size = new Vector3(collider.size.x, collider.size.y - ubytek, collider.size.z);

        var img = transform.Find("SC_Cube");
        img.localPosition = new Vector3(img.localPosition.x, img.localPosition.y - ubytek / 2, img.localPosition.z);
        img.localScale = new Vector3(img.localScale.x, img.localScale.y - ubytek, img.localScale.z);
    }

    private void CleanupSize()
    {
        if (!IsFullCell)
        {
            var prefab = Game.Instance.PrefabsStore.SandCombiner;
            Size = prefab.Size;
            var collider = GetComponent<BoxCollider>();
            var colliderP = prefab.GetComponent<BoxCollider>();
            collider.center = colliderP.center;
            collider.size = colliderP.size;

            var img = transform.Find("SC_Cube");
            var imgP = prefab.transform.Find("SC_Cube");
            img.localPosition = imgP.localPosition;
            img.localScale = imgP.localScale;
        }
    }

    internal void Collapse()
    {
        this.Plan(1);
    }

    void ISimpleTimerConsumer.OnTimer() => CollapseNow();

    public void CollapseNow()
    {
        var children = ListPool<Placeable>.Rent();
        transform.GetComponentsInLevel1Children(children);

        foreach (var p in children)
        {
            p.transform.SetParent(LevelGroup, true);
            p.AttachRigidBody(true, false);
        }
        children.Return();

        Kill();
    }

    public override void Cleanup(bool goesToInventory)
    {
        if (Collapsing)
            collapsingToken++;

        MassTransferer.Disconnect();

        base.Cleanup(goesToInventory);

        CleanupSize();
    }

    internal void ApplyVelocityThroughSandCombiner(Vector2 velocity, float sourceMass, VelocityFlags flags)
    {
        if (massTarget && massTarget.SpNodeIndex != 0)
        {
            float len = velocity.magnitude;
            if (len * sourceMass > PhysicsConsts.SandCombinerTransferMinimum && Vector2.Dot(velocity, Vector2.down) > 0.95f * len)
            {
                Game.Instance.StaticPhysics.ApplyTempForce(massTarget.SpNodeIndex, velocity, sourceMass, flags);
            }
        }
    }
}
