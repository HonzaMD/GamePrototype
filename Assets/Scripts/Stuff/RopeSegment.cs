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
        if (segCount <= 0)
            return;
        Vector3 segOffset = (end - start).normalized * segmentSize;

        Placeable prevNode = map.GetFirstTouching(start, Ksid.SpFixed, 0.05f);
        if (!prevNode)
            prevNode = map.GetFirstTouching(start, Ksid.SpNode, 0.05f);

        for (int i = 0; i < segCount; i++)
        {
            Placeable seg = Game.Instance.PrefabsStore.RopeSegment.CreateWithotInit(parent);
            seg.transform.position = start + i * segOffset;
            seg.transform.rotation = rotation;

            seg.Init(map);

            var joint = seg.GetComponent<HingeJoint>();
            if (prevNode)
            {
                if (!joint)
                    joint = CreateJoint(seg.gameObject, false);

                seg.CreateRbJoint(prevNode).SetupRb(joint, false);
                joint.connectedBody = prevNode.Rigidbody;
            }
            else
            {
                if (joint)
                    Destroy(joint);
            }

            prevNode = seg;
        }

        if (fixEnd)
        {
            ConnectEnd(end, prevNode, map);
        }
    }

    private static void ConnectEnd(Vector3 end, Placeable lastNode, Map map)
    {
        Placeable anchor = map.GetFirstTouching(end, Ksid.SpFixed, 0.05f);
        if (!anchor)
            anchor = map.GetFirstTouching(end, Ksid.SpNode, 0.05f);
        if (anchor)
        {
            HingeJoint j = CreateJoint(lastNode.gameObject, true);

            lastNode.CreateRbJoint(anchor).SetupRb(j, true);
            j.connectedBody = anchor.Rigidbody;
        }
    }

    private static HingeJoint CreateJoint(GameObject gameObject, bool anchorDown)
    {
        var j2 = Game.Instance.PrefabsStore.RopeSegment.GetComponent<HingeJoint>();
        var j = gameObject.AddComponent<HingeJoint>();
        j.axis = j2.axis;
        j.anchor = anchorDown ? Vector3.down * segmentSize : j2.anchor;
        j.autoConfigureConnectedAnchor = j2.autoConfigureConnectedAnchor;
        j.useSpring = j2.useSpring;
        j.spring = j2.spring;
        j.breakForce = j2.breakForce;
        j.breakTorque = j2.breakTorque;
        j.enablePreprocessing = j2.enablePreprocessing;
        return j;
    }
}

