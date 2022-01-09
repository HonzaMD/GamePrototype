using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Plank : Label
{
    public Vector3 Start;
    public Vector3 End;
    private Vector2 SegmentSize;
    private Vector2 RotatedSegmentSize;
    private Vector2 RotatedSegmentPos;

    public override Placeable PlaceableC => GetComponentInChildren<Placeable>();

    private void Awake()
    {
        SegmentSize = Game.Instance.PrefabsStore.LadderSegment.Size;
    }

    internal void AddToMap(Map map, Vector3 start, Vector3 end)
    {
        Start = start;
        End = end;
        Init(out var segmentCount, out var segmentsStart);

        for (int i = 0; i < segmentCount; i++)
        {
            SegmantAdd(map, segmentsStart, i);
        }
    }


    internal void Move(Map map, Vector3 start, Vector3 end)
    {
        Start = start;
        End = end;
        Init(out var segmentCount, out var segmentsStart);

        var segments = StaticList<PlankSegment>.List;
        GetComponentsInChildren(segments);
        for (int i = 0; i < segmentCount || i < segments.Count; i++)
        {
            if (i < segmentCount)
            {
                if (i < segments.Count)
                {
                    SegmentMove(map, segments[i], segmentsStart, i);
                }
                else
                {
                    SegmantAdd(map, segmentsStart, i);
                }
            }
            else
            {
                SegmentRemove(segments[i]);
            }
        }
        segments.Clear();
    }

    internal void RemoveFromMap()
    {
        var segments = StaticList<PlankSegment>.List;
        GetComponentsInChildren(segments);
        foreach (var seg in segments)
            SegmentRemove(seg);
        segments.Clear();
    }

    public override void Cleanup()
    {
        RemoveFromMap();
        base.Cleanup();
    }

    private void SegmantAdd(Map map, float segmentsStart, int i)
    {
        PlankSegment seg = Game.Instance.Pool.Get(Game.Instance.PrefabsStore.LadderSegment, transform, new Vector3(0, segmentsStart + i * SegmentSize.y, 0));
        seg.SegmentIndex = i;
        seg.PlaceToMap(map);
    }

    private void SegmentRemove(PlankSegment seg)
    {
        Game.Instance.Pool.Store(seg, Game.Instance.PrefabsStore.LadderSegment);
    }

    private void SegmentMove(Map map, PlankSegment seg, float segmentsStart, int i)
    {
        seg.transform.localPosition = new Vector3(0, segmentsStart + i * SegmentSize.y, 0);
        seg.KinematicMove(map);        
    }

    private void Init(out int segmentCount, out float segmentsStart)
    {
        if (Start.y > End.y)
        {
            var v = Start;
            Start = End;
            End = v;
        }

        var length = (End - Start).magnitude;
        segmentsStart = length % SegmentSize.y;
        if (segmentsStart > 0)
            segmentsStart -= SegmentSize.y;
        transform.rotation = Quaternion.FromToRotation(Vector3.up, End - Start);
        transform.position = Start;

        InitCollider(length, segmentsStart);
        InitRotatedSizes();

        segmentCount = Mathf.CeilToInt(length / SegmentSize.y);
    }

    private void InitCollider(float length, float segmentsStart)
    {
        var collider = GetComponent<BoxCollider>();
        var cSize = collider.size;
        cSize.y = length - segmentsStart;
        collider.size = cSize;
        var cCenter = collider.center;
        cCenter.y = (length + segmentsStart) / 2;
        collider.center = cCenter;
    }

    private void InitRotatedSizes()
    {
        var point2 = (transform.rotation * new Vector2(0, SegmentSize.y)).XY();
        RotatedSegmentSize = point2;
        RotatedSegmentPos = Vector2.zero;
        if (point2.x < 0)
        {
            RotatedSegmentSize.x = -point2.x;
            RotatedSegmentPos.x = point2.x;
        }
        if (RotatedSegmentSize.y < RotatedSegmentSize.x)
        {
            RotatedSegmentSize.y += SegmentSize.x;
            RotatedSegmentPos.y -= SegmentSize.x / 2;
        }
        else if (RotatedSegmentSize.x < RotatedSegmentSize.y)
        {
            RotatedSegmentSize.x += SegmentSize.x;
            RotatedSegmentPos.x -= SegmentSize.x / 2;
        }
    }

    internal void RefreshSegmentCoordinates(PlankSegment plankSegment)
    {
        plankSegment.PosOffset = RotatedSegmentPos;
        plankSegment.Size = RotatedSegmentSize;
    }
}

