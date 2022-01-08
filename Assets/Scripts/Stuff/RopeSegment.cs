using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class RopeSegment : Placeable
{
    public override void RefreshCoordinates()
    {
        var start = transform.position;
        var end = transform.TransformPoint(new Vector3(0, -Rope.segmentSize, 0));
        var dir = end - start;

        if (dir.x < 0)
        {
            PosOffset.x = dir.x;
            Size.x = -dir.x;
        }
        else
        {
            PosOffset.x = 0;
            Size.x = dir.x;
        }

        if (dir.y < 0)
        {
            PosOffset.y = dir.y;
            Size.y = -dir.y;
        }
        else
        {
            PosOffset.y = 0;
            Size.y = dir.y;
        }

        base.RefreshCoordinates();
    }
}

