using Assets.Scripts;
using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


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
    
    public bool IsMapPlaced => PlacedPosition != NotInMap;
    public int CellZ => transform.position.z < 0.25f ? 0 : 1;

    internal readonly static Vector2 NotInMap = new Vector2(-12345678f, 12345678f);

    public virtual void RefreshCoordinates()
    {
        PlacedPosition = Pivot + PosOffset;
        if (SubCellFlags != SubCellFlags.Free)
            CellBlocking = CellUtils.Combine(SubCellFlags, CellBlocking, transform);
    }

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
            AttachRigidBody();
    }

    public override void Cleanup()
    {
        Game.Map.Remove(this);
        if (TryGetComponent<IActiveObject>(out var ao))
        {
            Game.Instance.DeactivateObject(ao);
        }
        Game.Instance.RemoveMovingObject(this);
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

    public void AttachRigidBody()
    {
        if (HasRB)
            return;
        var rb = Game.Instance.PrefabsStore.RbBase.Create(transform.parent, transform.localPosition);
        rb.Rigidbody.mass = GetMass();
        transform.SetParent(rb.transform, true);
    }

    public void DetachRigidBody()
    {
        if (!HasRB)
            return;
        var rbLabel = this.Rigidbody.GetComponent<RbLabel>();
        if (!rbLabel || !transform.IsChildOf(rbLabel.transform))
            throw new InvalidOperationException("Tohle neni detachovatelne RB!");
        transform.SetParent(rbLabel.transform.parent, true);

        rbLabel.Kill();
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
        var center = (Pivot + PosOffset + Size * 0.5f).AddZ(newZ);
        return (!Physics.CheckBox(center, halfSize.AddZ(0.2f), Quaternion.identity, Game.Instance.CollisionLayaerMask));
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
}
