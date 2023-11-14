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

        for (int i = 0; i < segCount; i++)
        {
            Placeable seg = prototype.Create(parent);
            seg.transform.position = start + i * segOffset;
            seg.transform.rotation = rotation;

            //var joint = seg.GetComponent<HingeJoint>();
            //if (prevNode)
            //{
            //    seg.CreateRbJoint(prevNode).SetupJoint(joint, false);
            //    joint.connectedBody = prevNode.Rigidbody;
            //    //if (i == 0)
            //    //    joint.connectedAnchor = start;
            //    //else
            //    //    joint.connectedAnchor = new Vector3(0, i == 0 ? 0 : -segmentSize, 0);

            //}
            //else
            //{
            //    Destroy(joint);
            //}

            //prevNode = seg;
            seg.PlaceToMap(map);
        }
    }
}

