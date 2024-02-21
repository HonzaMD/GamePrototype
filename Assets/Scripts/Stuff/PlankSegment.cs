using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class PlankSegment : Placeable
{
    public override void RefreshCoordinates()
    {
        RefreshBounds((Placeable)Settings.Prototype);
        base.RefreshCoordinates();
    }


    internal static void AddToMap(Map map, PlankSegment prototype, Transform parent, Vector3 start, Vector3 end)
    {
        if (start.y > end.y)
            (start, end) = (end, start);

        float size = prototype.Size.y;

        var length = (end - start).magnitude;
        var normDir = (end - start) / length;
        var offset = length % size;
        if (offset > 0)
            start -= (size - offset) * normDir;
        
        var rotation = Quaternion.FromToRotation(Vector3.up, end - start);
        int segCount = Mathf.CeilToInt(length / size);
        Vector3 segOffset = normDir * size;

        var segments = ListPool<Placeable>.Rent();

        for (int i = 0; i < segCount; i++)
        {
            Placeable seg = prototype.Create(parent);
            seg.transform.position = start + i * segOffset;
            seg.transform.rotation = rotation;

            segments.Add(seg);
            seg.PlaceToMap(map);
        }

        Physics.SyncTransforms();

        var connector = new SpConnectionFinder(segments, map);
        connector.TryConnect();
    }
}

