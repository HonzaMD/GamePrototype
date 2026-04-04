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
        ApplyDamage(collision);
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

    private void ApplyDamage(Collision collision)
    {
        if (collision.contactCount == 0)
            return;

        if (!Label.TryFind(transform, out var myLabel) || !Label.TryFind(collision.collider.transform, out var otherLabel))
        {
            //Debug.LogWarning("pri kolizi nemam labely");
            return;
        }

        ApplyImpactDamage(collision, myLabel, otherLabel);
        ApplyKnifeDamage(collision, myLabel, otherLabel);
    }

    private void ApplyImpactDamage(Collision collision, Label myLabel, Label otherLabel)
    {

        var contact = collision.GetContact(0);
        // relativeVelocity = this - other, normal smeruje od other k this
        // priblizovaci rychlost = zaporna projekce
        float impactSpeed = Mathf.Max(0f, Vector3.Dot(collision.relativeVelocity, contact.normal));

        StaticBehaviour.ApplyImpactDamage(impactSpeed * impactSpeed, myLabel, otherLabel, false, contact.point);
    }

    private void ApplyKnifeDamage(Collision collision, Label myLabel, Label otherLabel)
    {
        StaticBehaviour.ApplyKnifeDamage(collision.relativeVelocity.sqrMagnitude, myLabel, otherLabel, collision.GetContact(0).point);
    }


    public static Vector3 RV;
}
