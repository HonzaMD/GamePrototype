using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;

public class CollisionForceToSp : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        if (Label.TryFind(collision.collider.transform, out var otherLabel))
        {
            TransferForce(collision, otherLabel);

            Placeable myLabel = GetComponent<Label>().PlaceableC;
            if (myLabel) 
            { 
                ApplyDamage(collision, myLabel, otherLabel);
                RaiseEnterEvents(collision, myLabel, otherLabel);
            }
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (Label.TryFind(collision.collider.transform, out var otherLabel))
        {
            TransferForce(collision, otherLabel);

            Placeable myLabel = GetComponent<Label>().PlaceableC;
            if (myLabel)
            {
                ApplyContactDamage(collision, myLabel, otherLabel);
                RaiseStayEvents(collision, myLabel, otherLabel);
            }
        }
    }

    private void TransferForce(Collision collision, Label otherLabel)
    {
        VelocityFlags flags = collision.relativeVelocity.sqrMagnitude > PhysicsConsts.ImpactVelocitySqr
            ? VelocityFlags.IsImpact | VelocityFlags.DontAffectRb
            : VelocityFlags.DontAffectRb;
        RV = collision.relativeVelocity;
        otherLabel.ApplyVelocity(collision.impulse * -0.1f, 10, flags);
    }

    private void ApplyDamage(Collision collision, Label myLabel, Label otherLabel)
    {
        if (collision.contactCount == 0)
            return;

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


    private void ApplyContactDamage(Collision collision, Label myLabel, Label otherLabel)
    {
        if (collision.contactCount == 0)
            return;

        var hitPos = collision.GetContact(0).point;
        if (!otherLabel.HasRB)
            StaticBehaviour.ApplyContactDamage(otherLabel, myLabel, hitPos);
        StaticBehaviour.ApplyContactDamage(myLabel, otherLabel, hitPos);
    }

    private void RaiseEnterEvents(Collision collision, Placeable myLabel, Label otherLabel)
    {
        if (myLabel.Settings.RecievesOnCollisionEnter)
        {
            if (myLabel.Settings.HasMultiplePhysicsEvents)
            {
                var components = ListPool<IPhysicsEvents>.Rent();
                myLabel.GetComponents(components);
                foreach (var c in components)
                    c.OnCollisionEnter(collision, otherLabel);
                components.Return();
            }
            else if (myLabel.TryGetComponent<IPhysicsEvents>(out var c))
            {
                c.OnCollisionEnter(collision, otherLabel);
            }
        }
    }

    private void RaiseStayEvents(Collision collision, Placeable myLabel, Label otherLabel)
    {
        if (myLabel.Settings.RecievesOnCollisionStay)
        {
            if (myLabel.Settings.HasMultiplePhysicsEvents)
            {
                var components = ListPool<IPhysicsEvents>.Rent();
                myLabel.GetComponents(components);
                foreach (var c in components)
                    c.OnCollisionStay(collision, otherLabel);
                components.Return();
            }
            else if (myLabel.TryGetComponent<IPhysicsEvents>(out var c))
            {
                c.OnCollisionStay(collision, otherLabel);
            }
        }
    }

    public static Vector3 RV;
}
