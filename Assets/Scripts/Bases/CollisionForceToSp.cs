using Assets.Scripts.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CollisionForceToSp : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        TransferForce(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        TransferForce(collision);
    }

    private void TransferForce(Collision collision)
    {
        if (Label.TryFind(collision.collider.transform, out var label))
        {
            label.ApplyVelocity(collision.impulse * -0.1f, 10, dontAffectRb: true);
        }
    }
}
