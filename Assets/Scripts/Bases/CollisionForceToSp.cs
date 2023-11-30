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
            VelocityFlags flags = collision.relativeVelocity.sqrMagnitude > PhysicsConsts.ImpactVelocitySqr 
                ? VelocityFlags.IsImpact | VelocityFlags.DontAffectRb
                : VelocityFlags.DontAffectRb;
            RV = collision.relativeVelocity;
            label.ApplyVelocity(collision.impulse * -0.1f, 10, flags);
        }
    }

    public static Vector3 RV;
}
