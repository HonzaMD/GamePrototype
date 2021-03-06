﻿using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rope : MonoBehaviour
{
    public Rigidbody StartAnchor;
    public HingeJoint EndAnchor;
    public RopeSegment FirstSegment;

    public const float segmentSize = 0.25f;

    internal void AddToMap(Map map, Vector3 start, Vector3 end, bool fixEnd)
    {
        StartAnchor.transform.position = start;
        EndAnchor.transform.position = end;

        StartAnchor.transform.rotation = Quaternion.FromToRotation(Vector3.down, end - start);
        EndAnchor.transform.rotation = Quaternion.FromToRotation(Vector3.up, start - end);

        if (!fixEnd)
            EndAnchor.gameObject.SetActive(false);

        int segCount = Mathf.CeilToInt((end - start).magnitude / segmentSize);
        Vector3 segOffset = (end - start).normalized * segmentSize;

        Rigidbody prevBody = StartAnchor;

        for (int i = 0; i < segCount; i++)
        {
            RopeSegment seg = i == 0 ? FirstSegment : Instantiate(FirstSegment, transform);
            seg.transform.position = start + i * segOffset;
            seg.transform.rotation = StartAnchor.transform.rotation;

            var joint = seg.GetComponent<HingeJoint>();
            joint.connectedBody = prevBody;
            joint.connectedAnchor = new Vector3(0, i == 0 ? 0 : -segmentSize, 0);
            prevBody = seg.GetComponent<Rigidbody>();

            map.Add(seg);
            Game.Instance.AddMovingObject(seg);
        }

        if (fixEnd)
        {
            EndAnchor.connectedBody = prevBody;
            EndAnchor.connectedAnchor = new Vector3(0, -segmentSize, 0);
        }
    }
}

