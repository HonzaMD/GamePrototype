using Assets.Scripts.Core;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class RopeSegment : Placeable
{
    public const float segmentSize = 0.25f;


    public override void RefreshCoordinates()
    {
        var start = transform.position;
        var end = transform.TransformPoint(new Vector3(0, -segmentSize, 0));
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


    public override void Cleanup()
    {
        GetComponent<Rigidbody>().Cleanup();
    }

    internal static void AddToMap(Map map, Transform parent, Vector3 start, Vector3 end, bool fixEnd)
    {
        Quaternion rotation = Quaternion.FromToRotation(Vector3.down, end - start);
        int segCount = Mathf.CeilToInt((end - start).magnitude / segmentSize);
        Vector3 segOffset = (end - start).normalized * segmentSize;

        Placeable prevNode = Game.Map.GetFirstTouching(start, Ksid.SpFixed, 0.05f);
        if (!prevNode)
            prevNode = Game.Map.GetFirstTouching(start, Ksid.SpNode, 0.05f);

        for (int i = 0; i < segCount; i++)
        {
            Placeable seg = Game.Instance.PrefabsStore.RopeSegment.Create(parent);
            seg.transform.position = start + i * segOffset;
            seg.transform.rotation = rotation;

            seg.PlaceToMap(map);

            var joint = seg.GetComponent<HingeJoint>();
            if (prevNode)
            {
                seg.CreateRbJoint(prevNode).SetupRb(joint, false);
                joint.connectedBody = prevNode.Rigidbody;
            }
            else
            {
                Destroy(joint);
            }

            prevNode = seg;
        }

        if (fixEnd)
        {
            ConnectEnd(end, prevNode);
        }
    }

    private static void ConnectEnd(Vector3 end, Placeable lastNode)
    {
        Placeable anchor = Game.Map.GetFirstTouching(end, Ksid.SpFixed, 0.05f);
        if (!anchor)
            anchor = Game.Map.GetFirstTouching(end, Ksid.SpNode, 0.05f);
        if (anchor)
        {
            var j1 = lastNode.GetComponent<HingeJoint>();
            var j = lastNode.gameObject.AddComponent<HingeJoint>();
            j.axis = j1.axis;
            j.anchor = Vector3.down * segmentSize;
            j.autoConfigureConnectedAnchor = j1.autoConfigureConnectedAnchor;
            j.useSpring = j1.useSpring;
            j.spring = j1.spring;
            j.breakForce = j1.breakForce;
            j.breakTorque = j1.breakTorque;
            j.enablePreprocessing = j1.enablePreprocessing;

            lastNode.CreateRbJoint(anchor).SetupRb(j, true);
            j.connectedBody = anchor.Rigidbody;
        }
    }
}

