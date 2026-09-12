using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Core.Inventory;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

// Sily: lokomoce, skok a podpora nohou, drag, presun po Z, reakcni sily, poskozeni koncetinou.
public abstract partial class ChLegsArms
{

    // Spolecny vypocet pro vsechny tri druhy poskozeni koncetinou.
    // Vraci false, kdyz koncetina na nicem nevisi.
    private bool TryGetLimbImpact(int index, out Label otherLabel, out float relSpeedSqr)
    {
        otherLabel = limbTargets[index];
        if (otherLabel == null)
        {
            relSpeedSqr = 0;
            return false;
        }
        relSpeedSqr = (body.linearVelocity - otherLabel.Velocity).sqrMagnitude;
        return true;
    }

    private void ApplyLimbImpactDamage(int index)
    {
        if (!TryGetLimbImpact(index, out var otherLabel, out float relSpeedSqr)) return;
        StaticBehaviour.ApplyImpactDamage(relSpeedSqr, placeable, otherLabel, true, Limbs[index].position);
    }

    private void ApplyLimbKnifeDamage(int index)
    {
        if (!TryGetLimbImpact(index, out var otherLabel, out float relSpeedSqr)) return;
        StaticBehaviour.ApplyKnifeDamageOneWay(relSpeedSqr, placeable, otherLabel, Limbs[index].position);
    }

    private void ApplyLimbContactDamage(int index)
    {
        var otherLabel = limbTargets[index];
        if (otherLabel == null) return;
        StaticBehaviour.ApplyContactDamageBidirectional(placeable, otherLabel, Limbs[index].position);
    }

    private void ApplyLimbsContactDamage()
    {
        for (int i = 0; i < limbStatus.Length; i++)
        {
            if (limbStatus[i] >= Catch)
                ApplyLimbContactDamage(i);
        }
    }

    // Pohon tela. Dve vetve: plny 2D regulator (visi na rukou nebo leti) / stoji na nohou
    // (jen horizontalne). Pise legUpDir, ktery pak cte ApplyJumpOrLegSupport.
    private void ApplyMoveForce(Vector2 groundVelocity)
    {
        if (ArmCatched || movementMode == MovementMode.Free)
        {
            var force = Vector2.ClampMagnitude(groundVelocity + desiredVelocity - body.linearVelocity.XY(), Settings.maxAcceleration);
            body.AddForce(force, ForceMode.VelocityChange);
            ApplyReactionToCaughtLimbs(force * 0.8f);
            legUpDir = Vector3.up;
        }
        else
        {
            Quaternion legRot = GetLegRotation();
            var xAxis = legRot * Vector3.right;
            legUpDir = legRot * Vector3.up;
            var xVel = Vector3.Dot(xAxis, body.linearVelocity);
            var xGVel = Vector3.Dot(xAxis, groundVelocity);
            var force = Mathf.Clamp(xGVel + desiredVelocity.x - xVel, -Settings.maxAcceleration, Settings.maxAcceleration);
            var forceVec = xAxis * force;
            body.AddForce(forceVec, ForceMode.VelocityChange);
            ApplyReactionToCaughtLimbs(forceVec * 0.8f);
        }
    }

    private void ApplyArmsCatchForce(Vector2 groundVelocity)
    {
        body.AddForce(GetArmsCatchForce(groundVelocity), ForceMode.VelocityChange);
    }

    // Vraci true, pokud se v tomto kroku odrazil skok - pak se neaplikuje drag.
    private bool ApplyJumpOrLegSupport()
    {
        if (desiredJump)
        {
            if (LegOnGround)
            {
                var jumpForce = Mathf.Sqrt(-2f * Physics.gravity.y * Settings.jumpHeight) - body.linearVelocity.y;
                body.AddForce(0, jumpForce, 0, ForceMode.VelocityChange);
                ApplyReactionToLegs(new Vector3(0, jumpForce, 0), true);
                desiredJump = false;
                DetachAllLegs();
                return true;
            }
        }
        else
        {
            Vector3 legF = legUpDir * Mathf.Max(GetLegForce(0), GetLegForce(1));
            body.AddForce(legF, ForceMode.VelocityChange);
            ApplyReactionToLegs(legF, null);
            float legSF = GetLegSideForce();
            body.AddForce(legSF, 0, 0, ForceMode.VelocityChange);
        }
        return false;
    }

    private void ApplyDrag()
    {
        body.AddForce(GetDrag());
    }

    // POZOR: teleportuje transform po ose Z, odpoji chycene koncetiny a prehmatne drzeny predmet.
    private void ApplyZMove()
    {
        if (desiredZMove == 0)
            return;

        var p = transform.position;
        if (placeable.CanZMove(p.z + desiredZMove))
        {
            p.z += desiredZMove;
            transform.position = p;
            var ho = GetHoldObject();
            if (ho != null)
                TryCorrectZPos(ho);
            DetachAllCaughtLimbs();
            MarkIdleLimbsFree();
            ResetHold();
        }
        desiredZMove = 0;
    }

    protected virtual void AdjustXOrientation()
    {
        if (body.linearVelocity.x > Settings.maxSpeed * 0.1f)
            lastXOrientation = 1;
        else if (body.linearVelocity.x < -Settings.maxSpeed * 0.1f)
            lastXOrientation = -1;
    }


    private Vector2 GetGroundVelocity()
    {
        int count = 0;
        Vector2 res = Vector2.zero;
        for (int f = 0; f < Limbs.Length; f++)
        {
            if (limbStatus[f] == Catch)
            {
                count++;
                res += limbTargets[f].Velocity.XY();
            }
        }

        if (count > 0)
            res /= count;
        return res;
    }

    private Quaternion GetLegRotation()
    {
        if (Physics.Raycast(LegSphere.transform.position, -legUpDir, out var hitInfo, LegSphere.radius * 1.3f, Settings.legStandLayerMask, QueryTriggerInteraction.Ignore))
        {
            if (hitInfo.normal.y >= Settings.minGroundDotProduct)
            {
                return Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
            }
        }

        return Quaternion.identity;
    }

    private Vector3 GetDrag()
    {
        return body.linearVelocity * -body.linearVelocity.magnitude * Settings.DragCoef;
    }

    private float GetLegForce(int index)
    {
        if (limbStatus[index] == Catch)
        {
            var legDir = LegSphere.transform.position.XY() - Limbs[index].position.XY();
            float force = (LegSphere.radius - legDir.magnitude) / LegSphere.radius;
            force *= Settings.LegForce;
            force += Vector3.Dot(legUpDir, body.linearVelocity) * -Settings.LegForceDampening;
            return force;
        }
        return 0;
    }

    private float GetLegSideForce()
    {
        float force = 0;
        force += GetLegSideForce(0);
        force += GetLegSideForce(1);
        float limit = Settings.LegSideLimit - Math.Min(Math.Abs(desiredVelocity.x), Settings.LegSideLimit);
        return Mathf.Clamp(force, -limit, limit);
    }

    private float GetLegSideForce(int index)
    {
        if (limbStatus[index] == Catch)
        {
            var delta = (LegSphere.transform.position.x - Limbs[index].position.x) / LegSphere.radius;
            return delta * delta * delta * Settings.LegSideLimit;
        }
        return 0;
    }

    private Vector2 GetArmsCatchForce(Vector2 groundVelocity)
    {
        var velocity = body.linearVelocity.XY();
        var localVelocity = velocity - groundVelocity;
        var dot = 1 - Mathf.Clamp(Vector2.Dot(localVelocity, desiredVelocity), 0, 1);
        Vector2 forceToReduce = dot * localVelocity;
        Vector2 result = Vector2.zero;
        GetArmCatchForce(2, ref forceToReduce, ref result, velocity);
        GetArmCatchForce(3, ref forceToReduce, ref result, velocity);
        return result;
    }

    private void GetArmCatchForce(int index, ref Vector2 forceToReduce, ref Vector2 result, Vector2 myVelocity)
    {
        if (limbStatus[index] == Catch)
        {
            var armDir = (ArmSphere.transform.position.XY() - Limbs[index].position.XY());
            var armDirNorm = armDir.normalized;
            var upModifier = Mathf.Clamp01(Vector2.Dot(Vector2.up, desiredVelocity)) * Mathf.Clamp01(Vector2.Dot(Vector2.up, armDirNorm));
            var holdVel = Vector2.Dot(armDirNorm, forceToReduce) + 1 - upModifier;
            var inForce = armDir.sqrMagnitude / (ArmSphere.radius * ArmSphere.radius);
            inForce = Mathf.Max(0, inForce - 0.7f * 0.7f);
            inForce *= Settings.ArmInForceCoef * holdVel;
            var armForce = -inForce * armDirNorm;
            result += armForce;
            forceToReduce += armForce;

            var relVelocity = Vector2.Dot(armDirNorm, limbTargets[index].Velocity.XY() - myVelocity);
            bool isImpact = relVelocity * relVelocity > PhysicsConsts.ImpactVelocitySqr;
            ApplyReactionToLimb(armForce, index, isImpact);
        }
    }

    private void ApplyReactionToCaughtLimbs(Vector3 velocity)
    {
        int count = 0;
        foreach (var status in limbStatus)
            if (status == Catch)
                count++;

        if (count > 0)
        {
            for (int f = 0; f < limbStatus.Length; f++)
            {
                if (limbStatus[f] == Catch)
                    ApplyReactionToLimb(velocity / count, f, null);
            }
        }
    }


    private void ApplyReactionToLegs(Vector3 velocity, bool? isImpact)
    {
        if (limbStatus[0] == Catch)
        {
            if (limbStatus[1] == Catch)
            {
                ApplyReactionToLimb(velocity * 0.5f, 0, isImpact);
                ApplyReactionToLimb(velocity * 0.5f, 1, isImpact);
            }
            else
            {
                ApplyReactionToLimb(velocity, 0, isImpact);
            }
        }
        else if (limbStatus[1] == Catch)
        {
            ApplyReactionToLimb(velocity, 1, isImpact);
        }
    }

    private void ApplyReactionToLimb(Vector3 vector3, int index, bool? isImpact)
    {
        if (isImpact == null)
        {
            var relVelocity = body.linearVelocity.XY() - limbTargets[index].Velocity.XY();
            isImpact = relVelocity.sqrMagnitude > PhysicsConsts.ImpactVelocitySqr;
        }
        limbTargets[index].ApplyVelocity(-vector3, body.mass, isImpact.Value ? VelocityFlags.IsImpact : VelocityFlags.None);
    }
}
