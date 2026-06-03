using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SandCombiner : Placeable, ISimpleTimerConsumer, IActiveObject20Sec, ILevelPlaceabe
{
    [HideInInspector]
    public int L1;
    [HideInInspector]
    public int L4;

    public Connectable MassTransferer;
    private float mass;
    private Placeable massTarget;

    private int collapsingToken;

    private static readonly Vector2Int[] N4 = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };

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

        Game.Instance.ActivateObject(this);
    }

    public void InitFull(Map map)
    {
        L1 = 4;
        L4 = 4;
        mass = 0;

        SubCellFlags = SubCellFlags.Full | SubCellFlags.Sand;

        PlaceToMap(map, false);

        for (int f = 0; f < transform.childCount; f++)
        {
            if (transform.GetChild(f).TryGetComponent(out Placeable p))
            {
                mass += p.GetMass();
            }
        }

        TryTransferMass(map);
        map.AddCellStateTest(map.WorldToCell(Pivot), CellZ == 0 ? CellStateCahnge.FreeSand0 : CellStateCahnge.FreeSand1);

        Game.Instance.ActivateObject(this);
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

        Game.Instance.DeactivateObject(this);

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

    public void GameUpdate20Sec()
    {
        if (Collapsing)
            return;

        var map = GetMap();

        if (!massTarget)
            TryTransferMass(map);

        var toKill = ListPool<Placeable>.Rent();
        if (CanTurnIntoDirt(map, toKill))
            TurnIntoBasicDirt(map, toKill);
        toKill.Return();
    }

    private bool CanTurnIntoDirt(Map map, List<Placeable> toKill)
    {
        int cellz = CellZ;
        var myCell = map.WorldToCell(Pivot);

        int fullOrSandCount = 0;
        int elementCount = map.CellSim.GetElement(myCell);
        foreach (var off in N4)
        {
            var flags = map.GetCellBlocking(myCell + off);
            if (flags.HasSubFlag(SubCellFlags.FullEx, cellz) || flags.HasSubFlag(SubCellFlags.Sand, cellz))
                fullOrSandCount++;
            elementCount += map.CellSim.GetElement(myCell + off);
        }
        if (fullOrSandCount < 4 || elementCount < 4)
            return false;

        var list = ListPool<Placeable>.Rent();
        int tag = 0;
        bool foundDirt = false;
        foreach (var off in N4)
        {
            list.Clear();
            map.Get(list, myCell + off, Ksid.Dirt, ref tag);
            foreach (var p in list)
            {
                if (p.IsStatic && p.CellBlocking.HasSubFlag(SubCellFlags.FullEx, cellz))
                {
                    foundDirt = true;
                    break;
                }
            }
            if (foundDirt)
                break;
        }
        list.Return();

        if (!foundDirt)
            return false;

        ref var cell = ref map.GetCell(myCell);
        foreach (var p in cell)
        {
            if ((p.CellBlocking & CellFlags.AllPartCells) == 0)
                continue;

            if (p is SandCombiner || p.Ksid.IsChildOfOrEq(Ksid.SandLike))
            {
                toKill.Add(p);
            }
            else
            {
                return false;
            }
        }
        return true;
    }

    private void TurnIntoBasicDirt(Map map, List<Placeable> toKill)
    {
        var myCell = map.WorldToCell(Pivot);
        Vector3 dirtPos = map.CellToWorld(myCell);
        Transform parent = LevelGroup;

        foreach (var p in toKill)
            if (p.IsAlive)
                p.Kill();

        var dirt = Game.Instance.PrefabsStore.BasicDirt.Create(parent, dirtPos, map);

        map.CellSim.AddElement(myCell, -4);

        var spNeighbors = ListPool<Placeable>.Rent();
        int tag = 0;
        foreach (var off in N4)
            map.Get(spNeighbors, myCell + off, Ksid.SpNode, ref tag);

        foreach (var c in spNeighbors)
        {
            if ((c.CellBlocking & CellFlags.AllFullEx) != 0
                && (c.SpNodeIndex != 0 || c.Ksid.IsChildOfOrEq(Ksid.SpFixed)))
            {
                dirt.CreateRbJoint(c).SetupSp();
            }
        }
        spNeighbors.Return();
    }

    void ILevelPlaceabe.Instantiate(Map map, Transform parent, Vector3 pos)
    {
        var p = Instantiate(this, parent);
        p.SetPlacedPosition(pos);
        p.InitFull(map);
    }
    bool ILevelPlaceabe.SecondPhase => false;
    Placeable ILevelPlaceabe.Prototype => this;
}
