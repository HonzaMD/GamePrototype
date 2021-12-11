using Assets.Scripts;
using Assets.Scripts.Core;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[Flags]
public enum CellBlocking
{
    Free            = 0b0000,
    Trigger         = 0b0001,

    Cell0     = 0b0000110000,
    Cell0Part = 0b0000010000,
    Cell1     = 0b0011000000,
    Cell1Part = 0b0001000000,
    Cell2     = 0b1100000000,
    Cell2Part = 0b0100000000,

    AllCells = Cell0 | Cell1 | Cell2,
    AllPartCells = Cell0Part | Cell1Part | Cell2Part,
}

public class Placeable : Label, ILevelPlaceabe
{
    public Vector2 PosOffset;
    [HideInInspector]
    public Vector2 PlacedPosition;
    public Vector2 Size = new Vector2(0.5f, 0.5f);
    public new Ksid Ksid;
    public CellBlocking CellBlocking = CellBlocking.AllCells;
    [HideInInspector, NonSerialized]
    public int Tag;

    public virtual void RefreshCoordinates()
    {
        PlacedPosition = transform.position.XY() + PosOffset;
    }

    public virtual Ksid TriggerTargets => Ksid.Unknown;
    public virtual void AddTarget(Placeable p) { }
    public virtual void RemoveTarget(Placeable p) { }

    void ILevelPlaceabe.Instantiate(Map map, Transform parent, Vector3 pos)
    {
        var p = Instantiate(this, parent);
        if (PosOffset.x < 0 || PosOffset.y < 0)
            pos += new Vector3(0.25f, 0.25f, 0);
        p.transform.localPosition = pos;
        map.Add(p);
        if (p.TryGetComponent<IActiveObject>(out var ao))
        {
            Game.Instance.ActivateObject(ao);
        }
        else if (p.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic)
        {
            Game.Instance.AddMovingObject(p);
        }
    }

    public bool IsTrigger => (CellBlocking & CellBlocking.Trigger) != 0;

    public override Placeable PlaceableC => this;

    public void UpdateMapPosIfMoved(Map map)
    {
        if ((transform.position.XY() + PosOffset - PlacedPosition).sqrMagnitude > 0.1f * 0.1f)
            map.Move(this);
    }
}
