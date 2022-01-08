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
        SegmentSize = GetComponentInChildren<PlankSegment>().Size;
    }

    internal void AddToMap(Map map, Vector3 start, Vector3 end)
    {
        Start = start;
        End = end;
        Init(out var segmentCount, out var segmentsStart);

        var segments = GetComponentsInChildren<PlankSegment>();
        for (int i = 0; i < segmentCount; i++)
        {
            PlankSegment seg = i < segments.Length ? segments[i] : Instantiate(segments[0], transform);
            seg.SegmentIndex = i;
            var pos = segments[0].transform.localPosition;
            pos.y = segmentsStart + i * SegmentSize.y;
            seg.transform.localPosition = pos;
            SegmentAdd(map, seg);
        }
    }

    internal void Move(Map map, Vector3 start, Vector3 end)
    {
        Start = start;
        End = end;
        Init(out var segmentCount, out var segmentsStart);

        var segments = GetComponentsInChildren<PlankSegment>();
        for (int i = 0; i < segmentCount || i < segments.Length; i++)
        {
            if (i < segmentCount)
            {
                PlankSegment seg = i < segments.Length ? segments[i] : Instantiate(segments[0], transform);
                seg.SegmentIndex = i;
                var pos = segments[0].transform.localPosition;
                pos.y = segmentsStart + i * SegmentSize.y;
                seg.transform.localPosition = pos;
                SegmentMove(map, seg);
            }
            else
            {
                var seg = segments[i];
                SegmentRemove(map, seg);
                if (i > 0)
                    Destroy(seg.gameObject);
            }
        }
    }

    internal void RemoveFromMap(Map map)
    {
        var segments = GetComponentsInChildren<PlankSegment>();
        for (int i = 0; i < segments.Length; i++)
        {
            var seg = segments[i];
            SegmentRemove(map, seg);
            if (i > 0)
                Destroy(seg.gameObject);
        }
    }

    protected virtual void SegmentAdd(Map map, PlankSegment seg)
    {
        map.Add(seg);
    }

    protected virtual void SegmentRemove(Map map, PlankSegment seg)
    {
        map.Remove(seg);
    }

    protected virtual void SegmentMove(Map map, PlankSegment seg)
    {
        map.Move(seg);
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

