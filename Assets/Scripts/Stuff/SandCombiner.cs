using Assets.Scripts.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SandCombiner : Placeable
{
    [HideInInspector]
    public int L1;
    [HideInInspector]
    public int L4;
    [HideInInspector]
    public bool IsFullCell;

    public void Init(int l1, int l4, bool isFullCell, IEnumerable<Placeable> children)
    {
        L1 = l1;
        L4 = l4;
        IsFullCell = isFullCell;

        SubCellFlags = isFullCell ? SubCellFlags.Full | SubCellFlags.Sand : SubCellFlags.Part | SubCellFlags.Sand;

        if (!IsFullCell)
            AdjustSize();

        PlaceToMap(Game.Map);

        foreach (var p in children)
        {
            if (p)
            {
                p.DetachRigidBody();
                p.transform.SetParent(transform, true);
            }
        }
    }

    private void AdjustSize()
    {
        var size = Math.Min(L1, L4) * (0.5f / 4f);
        var ubytek = 0.5f - size;
        Size.y = size;
        transform.localPosition += Vector3.up * ubytek;
        var collider = GetComponent<BoxCollider>();
        collider.center = new Vector3(collider.center.x, collider.center.y - ubytek / 2, collider.center.z);
        collider.size = new Vector3(collider.size.x, collider.size.y - ubytek, collider.size.z);

        var img = transform.Find("SC_Cube");
        img.localPosition = new Vector3(img.localPosition.x, img.localPosition.y - ubytek / 2, img.localPosition.z);
        img.localScale = new Vector3(img.localScale.x, img.localScale.y - ubytek, img.localScale.z);
    }
}
