using Assets.Scripts.Map;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ladder : Plank
{
    protected override void SegmentAdd(Map map, PlankSegment seg)
    {
        base.SegmentAdd(map, seg);
        foreach(var p in seg.GetComponentsInChildren<Placeable>())
        {
            map.Add(p);
        }
    }

    protected override void SegmentMove(Map map, PlankSegment seg)
    {
        base.SegmentMove(map, seg);
        foreach (var p in seg.GetComponentsInChildren<Placeable>())
        {
            map.Move(p);
        }
    }

    protected override void SegmentRemove(Map map, PlankSegment seg)
    {
        base.SegmentRemove(map, seg);
        foreach (var p in seg.GetComponentsInChildren<Placeable>())
        {
            map.Remove(p);
        }
    }
}
