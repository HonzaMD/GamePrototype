using Assets.Scripts.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class SandCombiner : Placeable
{
    public Transform ComboPrefab;
    private Rigidbody parent;
    private Rigidbody orignal;


    private void Awake()
    {
        parent = GetComponentInParent<Rigidbody>();
    }

    public override Rigidbody Rigidbody => parent;

    //public override void MovingTick()
    //{
    //    var rb = Rigidbody;
    //    if (orignal == null)
    //    {
    //        if (SleepCondition(rb))
    //        {
    //            foreach (var p in Game.Map.GetCell(transform.position.XY()))
    //            {
    //                if (p != this && p is SandCombiner sc)
    //                {
    //                    TryCombine(sc);
    //                }
    //            }
    //        }
    //    }
    //    else
    //    {
    //        if (WakeCondition(rb))
    //            Split();
    //    }
    //}

    private static bool WakeCondition(Rigidbody rb) => rb.velocity.sqrMagnitude > 0.05f || rb.maxAngularVelocity > 30;
    private static bool SleepCondition(Rigidbody rb) => rb.velocity.sqrMagnitude < 0.01f && rb.maxAngularVelocity < 10;

    private void Split()
    {
        orignal.transform.SetParent(parent.transform.parent, true);
        orignal.gameObject.SetActive(true);
        transform.SetParent(orignal.transform, true);
        parent.mass -= orignal.mass;
        orignal.velocity = parent.velocity;
        orignal.angularVelocity = parent.angularVelocity;
        if (parent.GetComponentInChildren<Collider>() == null)
            Destroy(parent.gameObject);
        parent = orignal;
        orignal = null;
    }

    private void TryCombine(SandCombiner sc)
    {
        if (SleepCondition(sc.Rigidbody))
        {
            if (orignal != null && sc.orignal == null)
            {
                sc.Combine(parent.transform);
            }
            else if (orignal == null)
            {
                if (sc.orignal == null)
                {
                    var combo = Instantiate(ComboPrefab, sc.parent.transform.parent);
                    combo.localPosition = sc.parent.transform.localPosition;
                    sc.Combine(combo);
                }
                Combine(sc.parent.transform);
            }
        }
    }

    private void Combine(Transform combo)
    {
        orignal = parent;
        transform.SetParent(combo, true);
        orignal.transform.SetParent(transform, true);
        parent = combo.GetComponent<Rigidbody>();
        parent.mass += orignal.mass;
        orignal.gameObject.SetActive(false);
    }
}
