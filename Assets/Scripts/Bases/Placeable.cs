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
    Full   = 0b0011,
    FullEx = 0b0010,
    Part   = 0b0001,
    Sand   = 0b0100,

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
    private int SpNodeIndex;

    public bool IsMapPlaced => PlacedPosition != NotInMap;

    internal readonly static Vector2 NotInMap = new Vector2(-12345678f, 12345678f);

    public virtual void RefreshCoordinates()
    {
        PlacedPosition = Pivot + PosOffset;
        if (SubCellFlags != SubCellFlags.Free)
            CellBlocking = CellUtils.Combine(SubCellFlags, CellBlocking, transform);
    }

    public Vector2 Center => Pivot + PosOffset + Size * 0.5f;

    public virtual Ksid TriggerTargets => Ksid.Unknown;
    public virtual void AddTarget(Placeable p) { }
    public virtual void RemoveTarget(Placeable p) { }

    public override Ksid KsidGet => Ksid;
    public override bool IsGroup => Settings?.HasSubPlaceables == true;
    public override Label Prototype => Settings?.Prototype;
    public override Placeable PlaceableC => this;
    public override bool CanBeKilled => !(Settings?.Unseparable == true);

    void ILevelPlaceabe.Instantiate(Map map, Transform parent, Vector3 pos)
    {
        var p = Instantiate(this, parent);
        p.LevelPlaceAfterInstanciate(map, pos);
    }

    public void LevelPlaceAfterInstanciate(Map map, Vector3 pos)
    {
        if (PosOffset.x < 0 || PosOffset.y < 0)
            pos += new Vector3(0.25f, 0.25f, 0);
        transform.localPosition = pos;
        PlaceToMap(map);
    }

    public void PlaceToMap(Map map)
    {
        AutoAttachRB();
        map.Add(this);
        if (TryGetComponent<IActiveObject>(out var ao))
        {
            Game.Instance.ActivateObject(ao);
        }
        else if (Rigidbody != null && !Rigidbody.isKinematic)
        {
            Game.Instance.AddMovingObject(this);
        }

        if (Settings?.HasSubPlaceables == true)
        {
            var placeables = ListPool<Placeable>.Rent();
            GetComponentsInChildren(placeables);
            foreach (var p in placeables)
                if (p != this)
                    p.PlaceToMap(map);
            placeables.Return();
        }
    }

    private void AutoAttachRB()
    {
        if (Settings?.AutoAtachRB == true && IsTopLabel)
            AttachRigidBody(true, false);
    }

    public override void Cleanup()
    {
        Game.Map.Remove(this);
        if (TryGetComponent<IActiveObject>(out var ao))
        {
            Game.Instance.DeactivateObject(ao);
        }
        Game.Instance.RemoveMovingObject(this);
        if (SpNodeIndex != 0)
        {
            Game.Instance.StaticPhysics.RemoveNode(SpNodeIndex);
            SpNodeIndex = 0;
        }
        base.Cleanup();
    }

    public void KinematicMove(Map map)
    {
        map.Move(this);
        if (Settings?.HasSubPlaceables == true)
        {
            var placeables = ListPool<Placeable>.Rent();
            GetComponentsInChildren(placeables);
            foreach (var p in placeables)
                if (p != this)
                    map.Move(p);
            placeables.Return();
        }
    }

    public bool IsTrigger => (CellBlocking & CellFlags.Trigger) != 0;


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
                if (startMoving)
                    rbLabel.StartMoving();
                if (incConnection)
                    rbLabel.ChengeConnectionCounter(1);
            }
        }
        else
        {
            Transform transform = KillableLabel().transform;
            var rbLabel = Game.Instance.PrefabsStore.RbBase.Create(transform.parent, transform.localPosition);
            var rb = rbLabel.Rigidbody;
            rb.mass = GetMass();
            rb.isKinematic = !startMoving;
            transform.SetParent(rbLabel.transform, true);
            if (incConnection)
                rbLabel.ChengeConnectionCounter(1);
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
                rbLabel.StopMoving();
            if (decConnection)
                rbLabel.ChengeConnectionCounter(-1);
        }
    }

    private float GetMass()
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
        var halfSize = Vector2.Max(Size * 0.5f - new Vector2(0.05f, 0.05f), new Vector2(0.02f, 0.02f));
        var center = Center.AddZ(newZ);
        return (!Physics.CheckBox(center, halfSize.AddZ(0.2f), Quaternion.identity, Game.Instance.CollisionLayaerMask));
    }

    private static Collider[] collidersBuff = new Collider[4];
    public bool CanZMove(float newZ, Label exception)
    {
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
        if (Settings?.HasSubPlaceables == true)
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

    internal virtual void MoveZ(float newZ)
    {
        var rb = Rigidbody;
        if (rb)
        {
            rb.transform.position = rb.transform.position.WithZ(newZ);
        }
        else
        {
            transform.position = transform.position.WithZ(newZ);
            TryCollapseSandCombiner();
        }
        KinematicMove(Game.Map);
    }

    internal void SpFall(int index)
    {
        if (SpNodeIndex == index)
            AttachRigidBody(true, false);
    }

    internal void SpConnectEdge(ref OutputCommand cmd)
    {
        if (SpNodeIndex == cmd.indexA && cmd.nodeB.SpNodeIndex == cmd.indexB)
        {
            RbJoint joint = CreateRbJoint(cmd.nodeB);
            if (!joint.Joint)
            {
                var j = Rigidbody.gameObject.AddComponent<FixedJoint>();
                j.breakForce = MathF.Min(cmd.compressLimit, cmd.stretchLimit);
                j.breakTorque = cmd.momentLimit;
                j.connectedBody = joint.OtherObj.Rigidbody;
                joint.SetupJoint(j);
                joint.OtherConnectable.SetupJoint(j);
            }
        }
    }

    private RbJoint CreateRbJoint(Placeable to)
    {
        var transform = ParentForConnections;
        for (int f = 0; f < transform.childCount; f++)
        {
            if (transform.GetChild(f).TryGetComponent(out RbJoint j))
            {
                if (j.OtherObj == to)
                    return j;
            }
        }
        
        RbJoint myJ = Game.Instance.PrefabsStore.RbJoint.CreateCL(transform);
        RbJoint otherJ = Game.Instance.PrefabsStore.RbJoint.CreateCL(to.ParentForConnections);
        myJ.Setup(this, to, otherJ);
        otherJ.Setup(to, this, myJ);

        return myJ;
    }

    internal void SpRemoveIndex(int index)
    {
        if (SpNodeIndex == index)
            SpNodeIndex = 0;
    }

    public void FindTouchingObjs(List<Placeable> output, Ksid ksid, float margin)
    {
        var marginVec = new Vector2(margin, margin);
        var placeables = ListPool<Placeable>.Rent();
        Game.Map.Get(placeables, PlacedPosition - marginVec, Size + 2 * marginVec, ksid);

        foreach (Placeable p in placeables)
        {
            if (p != this && Touches(p, margin))
                output.Add(p);
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
}
