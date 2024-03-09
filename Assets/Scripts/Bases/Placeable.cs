using Assets.Scripts;
using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Core.StaticPhysics;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SocialPlatforms;


[Flags]
public enum SubCellFlags : byte
{
    Free   = 0b0000,
    FullEx = 0b0010,
    Part   = 0b0001,
    Sand   = 0b0100,
    Full   = 0b0011,

    HasFloor = 0b0110,
}

[Flags]
public enum CellFlags
{
    Free              = 0b0000,
    Trigger           = 0b0001,

    Cell0     = SubCellFlags.Full << CellUtils.Cell0Shift,
    Cell0Part = SubCellFlags.Part << CellUtils.Cell0Shift,
    Cell0Sand = SubCellFlags.Sand << CellUtils.Cell0Shift,
    
    Cell1     = SubCellFlags.Full << CellUtils.Cell1Shift,
    Cell1Part = SubCellFlags.Part << CellUtils.Cell1Shift,
    Cell1Sand = SubCellFlags.Sand << CellUtils.Cell1Shift,

    AllCells = Cell0 | Cell1,
    AllPartCells = Cell0Part | Cell1Part,
}

public class Placeable : Label, ILevelPlaceabe
{
    public Vector2 PosOffset;
    [HideInInspector]
    public Vector2 PlacedPosition = NotInMap;
    public Vector2 Size = new Vector2(0.5f, 0.5f);
    public Ksid Ksid;
    public CellFlags CellBlocking;
    public SubCellFlags SubCellFlags;
    [HideInInspector, NonSerialized]
    public int Tag;
    public PlaceableSettings Settings;
    private int spNodeIndex;

    public bool IsMapPlaced => PlacedPosition != NotInMap;
    public override bool IsAlive => IsMapPlaced;

    internal readonly static Vector2 NotInMap = new Vector2(-12345678f, 12345678f);

    public virtual void RefreshCoordinates()
    {
        PlacedPosition = Pivot + PosOffset;
        if (SubCellFlags != SubCellFlags.Free)
            CellBlocking = CellUtils.Combine(SubCellFlags, CellBlocking, transform);
    }

    public Vector2 Center => Pivot + PosOffset + Size * 0.5f;
    public Vector3 Center3D => transform.position + (Vector3)(PosOffset + Size * 0.5f);

    public virtual Ksid TriggerTargets => Ksid.Unknown;
    public virtual void AddTarget(Placeable p) { }
    public virtual void RemoveTarget(Placeable p) { }

    public override Ksid KsidGet => Ksid;
    public override bool IsGroup => Settings?.HasSubPlaceables == true;
    public override Label Prototype => Settings?.Prototype;
    public override Placeable PlaceableC => this;
    public override bool CanBeKilled => !(Settings?.Unseparable == true);
    public virtual (float StretchLimit, float CompressLimit, float MomentLimit) SpLimits => (Settings.SpStretchLimit, Settings.SpCompressLimit, Settings.SpMomentLimit);
    protected virtual void AfterMapPlaced(Map map) { }

    public bool IsTrigger => (CellBlocking & CellFlags.Trigger) != 0;
    public int SpNodeIndex => spNodeIndex;

    void ILevelPlaceabe.Instantiate(Map map, Transform parent, Vector3 pos)
    {
        var p = Instantiate(this, parent);
        p.LevelPlaceAfterInstanciate(map, pos);
    }
    bool ILevelPlaceabe.SecondPhase => false;


    public void LevelPlaceAfterInstanciate(Map map, Vector3 pos)
    {
        SetPlacedPosition(pos);
        PlaceToMap(map);
    }

    public void SetPlacedPosition(Vector3 pos)
    {
        if (PosOffset.x < 0 || PosOffset.y < 0)
            pos += new Vector3(0.25f, 0.25f, 0);
        transform.localPosition = pos;
    }

    public override void Init(Map map) => PlaceToMap(map);

    public void PlaceToMap(Map map)
    {
        AutoAttachRB();
        map.Add(this);
        if (TryGetComponent<IActiveObject>(out var ao))
        {
            Game.Instance.ActivateObject(ao);
        }
        else if (HasActiveRB)
        {
            Game.Instance.AddMovingObject(this, map);
        }

        if (IsGroup)
        {
            var placeables = ListPool<Placeable>.Rent();
            GetComponentsInChildren(placeables);
            foreach (var p in placeables)
                if (p != this)
                    p.PlaceToMap(map);
            placeables.Return();
        }

        AfterMapPlaced(map);
    }

    private void AutoAttachRB()
    {
        if (Settings?.AutoAtachRB == true && IsTopLabel)
            AttachRigidBody(true, false);
    }

    public override void Cleanup()
    {
        GetMap().Remove(this);
        if (TryGetComponent<IActiveObject>(out var ao))
        {
            Game.Instance.DeactivateObject(ao);
        }
        Game.Instance.RemoveMovingObject(this);
        if (spNodeIndex != 0)
        {
            Game.Instance.StaticPhysics.RemoveNode(spNodeIndex);
            spNodeIndex = 0;
        }
        base.Cleanup();
    }

    public void KinematicMove(Map map)
    {
        map.Move(this);
        if (IsGroup)
        {
            var placeables = ListPool<Placeable>.Rent();
            GetComponentsInChildren(placeables);
            foreach (var p in placeables)
                if (p != this)
                    map.Move(p);
            placeables.Return();
        }
    }


    public void UpdateMapPosIfMoved(Map map)
    {
        if ((transform.position.XY() + PosOffset - PlacedPosition).sqrMagnitude > 0.1f * 0.1f)
            map.Move(this);
    }

    public void AttachRigidBody(bool startMoving, bool incConnection)
    {
        BefereAtachRigidBody();
        if (HasRB)
        {
            if (TryGetRbLabel(out var rbLabel))
            {
                rbLabel.Init(startMoving, false, incConnection);
            }
        }
        else
        {
            Transform transform = KillableLabel().transform;
            var rbLabel = Game.Instance.PrefabsStore.RbBase.Create(transform.parent, transform.localPosition, null);
            var rb = rbLabel.Rigidbody;
            transform.SetParent(rbLabel.transform, true);
            rb.mass = rbLabel.PlaceableC.GetMass();
            rbLabel.Init(startMoving, !startMoving, incConnection);
        }
    }

    public void RegisterMovingObjRecursivelly()
    {
        if (IsMapPlaced)
        {
            var map = GetMap();
            Game.Instance.AddMovingObject(this, map);
            if (IsGroup)
            {
                var placeables = ListPool<Placeable>.Rent();
                GetComponentsInChildren(placeables);
                foreach (var p in placeables)
                    if (p != this)
                        Game.Instance.AddMovingObject(p, map);
                placeables.Return();
            }
        }
    }

    public void UnRegisterMovingObjRecursivelly()
    {
        if (IsMapPlaced)
        {
            Game.Instance.RemoveMovingObject(this);
            if (IsGroup)
            {
                var placeables = ListPool<Placeable>.Rent();
                GetComponentsInChildren(placeables);
                foreach (var p in placeables)
                    if (p != this)
                        Game.Instance.RemoveMovingObject(p);
                placeables.Return();
            }
        }
    }

    public virtual void BefereAtachRigidBody()
    {
        TryCollapseSandCombiner();
    }

    private void TryCollapseSandCombiner()
    {
        if (Ksid.IsChildOf(Ksid.SandLike) && TryGetParentLabel(out var pl) && pl is SandCombiner sandCombiner)
        {
            sandCombiner.CollapseNow();
        }
    }

    public void DetachRigidBody(bool stopMoving, bool decConnection)
    {
        if (TryGetRbLabel(out var rbLabel))
        {
            if (stopMoving)
            {
                rbLabel.StopMoving();
            }
            if (decConnection)
                rbLabel.ChengeConnectionCounter(-1);
        }
    }

    public override float GetMass()
    {
        var settings = Settings;
        if (!settings)
            settings = Game.Instance.DefaultPlaceableSettings;

        if (settings.Mass > 0)
            return settings.Mass;

        switch (settings.DensityMode)
        {
            case DensityMode.BoundingBox:
                {
                    float vol = Size.x * Size.y * Mathf.Min(Size.x, Size.y);
                    return vol * settings.Density;
                }
            case DensityMode.Circle:
                {
                    float radius = Mathf.Min(Size.x, Size.y) / 2;
                    float vol = Mathf.PI * 4 * radius * radius * radius / 3;
                    return vol * settings.Density;
                }
            default:
                throw new InvalidOperationException("Neplatny case");
        }
    }

    public bool CanZMove(float newZ)
    {
        if (HasRbJoints())
            return false;
        var halfSize = Vector2.Max(Size * 0.5f - new Vector2(0.05f, 0.05f), new Vector2(0.02f, 0.02f));
        var center = Center.AddZ(newZ);
        return (!Physics.CheckBox(center, halfSize.AddZ(0.2f), Quaternion.identity, Game.Instance.CollisionLayaerMask));
    }

    private static Collider[] collidersBuff = new Collider[4];
    public bool CanZMove(float newZ, Label exception)
    {
        if (HasRbJoints())
            return false;
        var halfSize = Vector2.Max(Size * 0.5f - new Vector2(0.05f, 0.05f), new Vector2(0.02f, 0.02f));
        var center = Center.AddZ(newZ);
        int count = Physics.OverlapBoxNonAlloc(center, halfSize.AddZ(0.2f), collidersBuff, Quaternion.identity, Game.Instance.CollisionLayaerMask);
        if (count == 0)
            return true;
        if (count == collidersBuff.Length)
            return false;

        bool ok = true;
        for (int f = 0; f < count; f++)
        {
            if (!ok || !Label.TryFind(collidersBuff[f].transform, out var l) || l != exception)
                ok = false;
            collidersBuff[f] = null;
        }
        return ok;
    }

    public void SetTagRecursive(int tag)
    {
        if (IsGroup)
        {
            var placeables = ListPool<Placeable>.Rent();
            GetComponentsInChildren(placeables);
            foreach (var p in placeables)
                p.Tag = tag;
        }
        else
        {
            Tag = tag;
        }
    }

    internal virtual void MoveZ(float newZ, Map map)
    {
        var rb = Rigidbody;
        if (rb)
        {
            rb.transform.position = rb.transform.position.WithZ(newZ);
        }
        else
        {
            if (!CanBeKilled)
                Debug.LogError("Hejbes necim co neni samostatna entita");
            transform.position = transform.position.WithZ(newZ);
            TryCollapseSandCombiner();
        }
        KinematicMove(map);
    }

    internal void SpFall(int index)
    {
        if (spNodeIndex == index)
            AttachRigidBody(true, false);
    }

    internal void SpConnectEdgeAsRb(ref OutputCommand cmd)
    {
        if (spNodeIndex == cmd.indexA && cmd.nodeB.spNodeIndex == cmd.indexB)
        {
            RbJoint joint = CreateRbJoint(cmd.nodeB);
            joint.ClearSp();
            if (joint.state == RbJoint.State.None)
            {
                joint.SetupRb1(true);
                var j = Rigidbody.gameObject.AddComponent<FixedJoint>();
                j.breakForce = MathF.Min(cmd.compressLimit, cmd.stretchLimit) * PhysicsConsts.SpToRbLimitsMultiplier;
                j.breakTorque = cmd.momentLimit * PhysicsConsts.SpToRbLimitsMultiplier;
                //j.breakForce = float.PositiveInfinity;
                //j.breakTorque = float.PositiveInfinity;
                j.connectedBody = joint.OtherObj.Rigidbody;
                joint.SetupRb2(j);
            }
        }
    }

    internal void SpBreakEdge(ref OutputCommand cmd)
    {
        if (spNodeIndex == cmd.indexA && cmd.nodeB.spNodeIndex == cmd.indexB && TryFindRbJoint(cmd.nodeB, out var j))
        {
            var pos = Center3D + (cmd.nodeB.Center3D - Center3D) * 0.5f;
            var particleEffect = Game.Instance.PrefabsStore.ParticleEffect.CreateWithotInit(LevelGroup, pos);
            particleEffect.Init(10);

            j.ClearSp();
            j.Disconnect();
        }
    }

    public bool TryFindRbJoint(Placeable to, out RbJoint output)
    {
        var transform = ParentForConnections;
        for (int f = 0; f < transform.childCount; f++)
        {
            if (transform.GetChild(f).TryGetComponent(out output))
            {
                if (output.OtherObj == to)
                    return true;
            }
        }

        output = null;
        return false;
    }

    public bool HasRbJoints()
    {
        var transform = ParentForConnections;
        for (int f = 0; f < transform.childCount; f++)
        {
            if (transform.GetChild(f).TryGetComponent(out RbJoint j))
            {
                if (j.IsConnected)
                    return true;
            }
        }
        return false;
    }

    public RbJoint CreateRbJoint(Placeable to)
    {
        if (TryFindRbJoint(to, out var j))
            return j;
        
        RbJoint myJ = Game.Instance.PrefabsStore.RbJoint.CreateCL(ParentForConnections);
        RbJoint otherJ = Game.Instance.PrefabsStore.RbJoint.CreateCL(to.ParentForConnections);
        myJ.Setup(this, to, otherJ);
        otherJ.Setup(to, this, myJ);

        return myJ;
    }

    internal void SpRemoveIndex(int index)
    {
        if (spNodeIndex == index)
        {
            spNodeIndex = 0;
            DisconnectConnectables(ConnectableType.MassTransfer);
        }
    }

    public void FindTouchingObjs(Map map, List<Placeable> output, Ksid ksid, float margin, int tag = 0)
    {
        var marginVec = new Vector2(margin, margin);
        var placeables = ListPool<Placeable>.Rent();
        map.Get(placeables, PlacedPosition - marginVec, Size + 2 * marginVec, ksid, tag);

        foreach (Placeable p in placeables)
        {
            if (p != this && Touches(p, margin))
                output.Add(p);
            else
                p.Tag = 0; // abych to nasel pri hledani z jinych mist
        }

        placeables.Return();
    }

    public bool Touches(Placeable p, float margin)
    {
        var colliders1 = p.GetCollidersBuff1();
        var colliders2 = GetCollidersBuff2();

        foreach (var c1 in colliders1)
            foreach (var c2 in colliders2)
                if (c1.Touches(c2, margin))
                {
                    colliders1.Clear();
                    colliders2.Clear();
                    return true;
                }

        colliders1.Clear();
        colliders2.Clear();
        return false;
    }

    protected void RefreshBounds(Placeable prototype)
    {
        Vector3 offset = prototype.PosOffset;
        Vector3 size = prototype.Size;

        Vector2 start = transform.TransformPoint(offset);
        Vector2 p2 = transform.TransformPoint(offset + size);
        Vector2 p3 = transform.TransformPoint(offset.PlusX(size.x));
        Vector2 p4 = transform.TransformPoint(offset.PlusY(size.y));

        Vector2 end = start;

        TryExtendBounds(ref start, ref end, p2);
        TryExtendBounds(ref start, ref end, p3);
        TryExtendBounds(ref start, ref end, p4);

        PosOffset = start - Pivot;
        Size = end - start;
    }

    private static void TryExtendBounds(ref Vector2 start, ref Vector2 end, Vector2 v)
    {
        if (v.x < start.x)
            start.x = v.x;
        if (v.y < start.y)
            start.y = v.y;
        if (v.x > end.x)
            end.x = v.x;
        if (v.y > end.y)
            end.y = v.y;
    }

    internal void AddSpNode(ref InputCommand cmd)
    {
        spNodeIndex = Game.Instance.StaticPhysics.ReserveNodeIndex();
        cmd.indexA = spNodeIndex;
        cmd.nodeA = this;
        cmd.pointA = Center;
        var isFixed = Ksid.IsChildOfOrEq(Ksid.SpFixed);
        cmd.isAFixed = isFixed;
        if (!isFixed)
            cmd.forceA = Vector2.down * GetMass();
    }
}
