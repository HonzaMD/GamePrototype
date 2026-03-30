using Assets.Scripts.Bases;
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
        ApplyImpactDamage(collision);
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

    private void ApplyImpactDamage(Collision collision)
    {
        if (collision.contactCount == 0)
            return;

        if (!Label.TryFind(transform, out var myLabel) || !Label.TryFind(collision.collider.transform, out var otherLabel))
        {
            //Debug.LogWarning("pri kolizi nemam labely");
            return;
        }

        var normal = collision.GetContact(0).normal;
        // relativeVelocity = this - other, normal smeruje od other k this
        // priblizovaci rychlost = zaporna projekce
        float impactSpeed = Mathf.Max(0f, Vector3.Dot(collision.relativeVelocity, normal));

        StaticBehaviour.ApplyImpactDamage(impactSpeed * impactSpeed, myLabel, otherLabel, false);
    }


    public static Vector3 RV;
}
