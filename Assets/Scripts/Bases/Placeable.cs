using Assets.Scripts;
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
    Free = 0b0000,
    Full = 0b0011,
    Part = 0b0001,
    Sand = 0b0100,

    HasFloor = 0b0110,
}

[Flags]
public enum CellFlags
{
    Free              = 0b0000,
    Trigger           = 0b0001,

    Cell1     = SubCellFlags.Full << CellUtils.Cell1Shift,
    Cell1Part = SubCellFlags.Part << CellUtils.Cell1Shift,
    Cell1Sand = SubCellFlags.Sand << CellUtils.Cell1Shift,
    
    Cell2     = SubCellFlags.Full << CellUtils.Cell2Shift,
    Cell2Part = SubCellFlags.Part << CellUtils.Cell2Shift,
    Cell2Sand = SubCellFlags.Sand << CellUtils.Cell2Shift,

    AllCells = Cell1 | Cell2,
    AllPartCells = Cell1Part | Cell2Part,
}

public class Placeable : Label, ILevelPlaceabe
{
    public Vector2 PosOffset;
    [HideInInspector]
    public Vector2 PlacedPosition;
    public Vector2 Size = new Vector2(0.5f, 0.5f);
    public new Ksid Ksid;
    public CellFlags CellBlocking;
    public SubCellFlags SubCellFlags;
    [HideInInspector, NonSerialized]
    public int Tag;

    public virtual void RefreshCoordinates()
    {
        PlacedPosition = transform.position.XY() + PosOffset;
        if (SubCellFlags != SubCellFlags.Free)
            CellBlocking = CellUtils.Combine(SubCellFlags, CellBlocking, transform);
    }

    public virtual Ksid TriggerTargets => Ksid.Unknown;
    public virtual void AddTarget(Placeable p) { }
    public virtual void RemoveTarget(Placeable p) { }
//    public virtual void MovingTick() { }

    void ILevelPlaceabe.Instantiate(Map map, Transform parent, Vector3 pos)
    {
        var p = Instantiate(this, parent);
        if (PosOffset.x < 0 || PosOffset.y < 0)
            pos += new Vector3(0.25f, 0.25f, 0);
        p.transform.localPosition = pos;
        p.PlaceToMap(map);
    }

    public void PlaceToMap(Map map)
    {
        map.Add(this);
        if (TryGetComponent<IActiveObject>(out var ao))
        {
            Game.Instance.ActivateObject(ao);
        }
        else if (Rigidbody != null && !Rigidbody.isKinematic)
        {
            Game.Instance.AddMovingObject(this);
        }
    }

    public bool IsTrigger => (CellBlocking & CellFlags.Trigger) != 0;

    public override Placeable PlaceableC => this;

    public void UpdateMapPosIfMoved(Map map)
    {
        if ((transform.position.XY() + PosOffset - PlacedPosition).sqrMagnitude > 0.1f * 0.1f)
            map.Move(this);
    }
}
