using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class PlankSegment : Placeable
{
    public override void RefreshCoordinates()
    {
        var plank = GetComponentInParent<Plank>();
        plank.RefreshSegmentCoordinates(this);
        base.RefreshCoordinates();
    }

    public override Vector3 GetClosestPoint(Vector3 position) => GetComponentInParent<Collider>().ClosestPoint(position);
}

