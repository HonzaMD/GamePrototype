using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class PlankSegment : Placeable
{
    [HideInInspector]
    public int SegmentIndex;

    public override void RefreshCoordinates()
    {
        var plank = GetComponentInParent<Plank>();
        plank.RefreshSegmentCoordinates(this);
        base.RefreshCoordinates();
    }
}

