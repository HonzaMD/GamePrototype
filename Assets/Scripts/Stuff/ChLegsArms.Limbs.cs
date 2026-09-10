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

// Stav koncetin: casovace, vyber volne koncetiny, pripojovani a odpojovani, hledani opory.
public abstract partial class ChLegsArms
{

    private void DetachUnwantedLimbs(bool allowHoldDrop)
    {
        DetachLegIfNeeded(0);
        DetachLegIfNeeded(1);
        DetachArmIfNeeded(2, allowHoldDrop);   // POZOR: pri pusteni planuje ScheduleCollisionRestore
        DetachArmIfNeeded(3, allowHoldDrop);
    }

    private void TryCatchWithFreeLeg()
    {
        if (!desiredCrouch && Vector3.Dot(body.linearVelocity, legUpDir) <= 0 && TrySelectFreeLeg(out var index))
        {
            TryCatchLeg(index);
        }
    }

    private void TryCatchWithFreeArm()
    {
        if (desiredCatch && TrySelectFreeArm(out var index))
        {
            TryCatchArm(index);
        }
    }

    private void TickLimbTimers()
    {
        for (int f = 0; f < limbStatus.Length; f++)
        {
            if (limbStatus[f] <= Timeout)
                limbStatus[f] -= Time.fixedDeltaTime * Settings.LegTimeout;
        }
    }

    private void DetachLegIfNeeded(int index)
    {
        if (limbStatus[index] == Catch)
        {
            if (desiredCrouch)
            {
                DetachLimb(index);
            }
            else
            {
                var lpos = Limbs[index].position.XY();
                var center = LegSphere.transform.position.XY();
                var radius = desiredJump ? LegSphere.radius * 1.2f : LegSphere.radius;
                if ((lpos - center).sqrMagnitude > radius * radius)
                {
                    DetachLimb(index);
                }
                else if (limbStatus[PairedLimb(index)] == Catch)
                {
                    float otherX = Limbs[PairedLimb(index)].position.x;
                    if (lpos.x <= otherX && otherX < center.x && body.linearVelocity.x >= 0)
                        DetachLimb(index);
                    if (lpos.x >= otherX && otherX > center.x && body.linearVelocity.x <= 0)
                        DetachLimb(index);
                }
            }
        }
    }

    private void DetachArmIfNeeded(int index, bool allowHoldDrop)
    {
        if (limbStatus[index] == Catch)
        {
            if (!desiredCatch)
            {
                DetachLimb(index);
            }
            else
            {
                var lpos = Limbs[index].position.XY();
                var center = ArmSphere.transform.position.XY();
                var radius = ArmSphere.radius;
                if ((lpos - center).sqrMagnitude > radius * radius)
                {
                    DetachLimb(index);
                }
                else if (limbStatus[PairedLimb(index)] == Catch)
                {
                    var dotPos1 = Vector2.Dot(desiredVelocity, lpos - center);
                    if (dotPos1 < 0)
                    {
                        var dotPos2 = Vector2.Dot(desiredVelocity, Limbs[PairedLimb(index)].position.XY() - center);
                        if (dotPos1 <= dotPos2)
                            DetachLimb(index);
                    }
                }
            }
        }
        else if (limbStatus[index] == Hold)
        {
            if (!desiredHold)
            {
                ScheduleCollisionRestore(limbTargets[index]);
                DetachLimb(index);
            }
            else if (allowHoldDrop)
            {
                var lpos = Limbs[index].position.XY();
                var center = ArmSphere.transform.position.XY();
                var radius = ArmSphere.radius * 1.8f;
                if ((lpos - center).sqrMagnitude > radius * radius)
                {
                    ScheduleCollisionRestore(limbTargets[index]);
                    DetachLimb(index);
                }
            }
        }
        else if (limbStatus[index] == PickUp)
        {
            var lpos = Limbs[index].position.XY();
            var center = ArmSphere.transform.position.XY();
            var radius = ArmSphere.radius * 1.8f;
            var dist = (lpos - center).sqrMagnitude;
            if (dist > radius * radius)
            {
                limbStatus[index] = Timeout;
                ScheduleCollisionRestore(limbTargets[index]);
                DetachLimb(index);
            }
            var dest = ArmSphere.transform.position.XY() + ComputeHoldTarget(index);
            dist = (lpos - dest).sqrMagnitude;
            if (dist < 0.3f * 0.3f)
            {
                ScheduleCollisionRestore(limbTargets[index]);
                DetachLimb(index);
            }
        }
    }

    private int PairedLimb(int index) => index ^ 1;

    private void DetachLimb(int index)
    {
        limbConnectors[index].Disconnect();
        if (limbTargets[index] || limbStatus[index] > Timeout)
            Debug.LogError("Dosconnect se neudelal");
    }

    private Transform OnLimbDetached(int index)
    {
        if (limbStatus[index] == Hold && IsInventoryActive && !desiredHold)
            InventoryReturn();
        if (limbStatus[index] == PickUp)
            InventoryPickup(limbTargets[index]);
        limbStatus[index] = Timeout;
        limbTargets[index] = null;
        return transform;
    }

    private void DetachAllLegs()
    {
        if (limbStatus[0] == Catch)
            DetachLimb(0);
        if (limbStatus[1] == Catch)
            DetachLimb(1);
    }

    private void DetachAllCaughtLimbs()
    {
        for (int f = 0; f < Limbs.Length; f++)
        {
            if (limbStatus[f] == Catch)
                DetachLimb(f);
        }
    }

    private void DetachAllLimbs()
    {
        for (int f = 0; f < Limbs.Length; f++)
        {
            if (limbTargets[f] != null)
                DetachLimb(f);
        }
    }

    private void MarkIdleLimbsFree()
    {
        if (limbStatus[0] > Free && limbStatus[1] > Free && !LegOnGround)
        {
            if (limbStatus[0] < limbStatus[1] && limbStatus[0] <= Timeout)
            {
                limbStatus[0] = Free;
            }
            else if (limbStatus[1] <= Timeout)
            {
                limbStatus[1] = Free;
            }
        }

        if (limbStatus[2] > Free && limbStatus[3] > Free && !ArmCatched)
        {
            if (limbStatus[2] < limbStatus[3] && limbStatus[2] <= Timeout)
            {
                limbStatus[2] = Free;
            }
            else if (limbStatus[3] <= Timeout)
            {
                limbStatus[3] = Free;
            }
        }
    }

    private void TryCatchLeg(int index)
    {
        if (limbStatus[PairedLimb(index)] == Catch)
        {
            float otherX = Limbs[PairedLimb(index)].position.x;
            var centerX = LegSphere.transform.position.x;
            if (otherX > centerX && body.linearVelocity.x > 0)
                return;
            if (otherX < centerX && body.linearVelocity.x < 0)
                return;
        }

        Vector3 direction = Vector3.down * Settings.maxSpeed + body.linearVelocity;
        if (TryCatchLegAlong(index, direction, LegSphere.radius))
            return;

        float radius = desiredJump ? LegSphere.radius * 1.2f : LegSphere.radius * 0.7f;
        if (TryCatchLegAlong(index, Vector3.down, radius))
            return;

        if (desiredJump && limbStatus[PairedLimb(index)] != Catch)
        {
            direction = new Vector3(Mathf.Sign(body.linearVelocity.x) * -0.5f, -1f);
            if (TryCatchLegAlong(index, direction, radius))
                return;
        }
    }

    private bool TryCatchLegAlong(int index, Vector3 direction, float radius)
    {
        if (Physics.Raycast(LegSphere.transform.position, direction, out var hitInfo, radius, Settings.legStandLayerMask, QueryTriggerInteraction.Ignore))
        {
            if (hitInfo.normal.y >= Settings.minGroundDotProduct && TryAttachLimbTo(index, ref hitInfo))
            {
                PlaceLimbAtOwnZ(index, ref hitInfo);
                ApplyLimbImpactDamage(index);
                ApplyLimbKnifeDamage(index);
                return true;
            }
        }
        return false;
    }

    bool TryAttachLimbTo(int index, ref RaycastHit hitInfo, Label reqLabel = null)
    {
        if (Label.TryFind(hitInfo.collider.transform, out var label) && (reqLabel == null || label == reqLabel))
        {
            DisconnectTargetsOwnJoints(label);
            limbTargets[index] = label;
            limbConnectors[index].ConnectTo(label, ConnectableType.LegArm, true);
            return true;
        }
        return false;
    }

    private void DisconnectTargetsOwnJoints(Label label)
    {
        if (label.KsidGet.IsChildOf(Ksid.DisconnectedByCatch) && label.TryGetComponent<IConnector>(out var connector))
            connector.Disconnect(placeable);
    }

    private void PlaceLimbAtOwnZ(int index, ref RaycastHit hitInfo)
    {
        Limbs[index].position = new Vector3(hitInfo.point.x, hitInfo.point.y, Settings.limbZ[index] + transform.position.z);
        Limbs[index].rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
        limbStatus[index] = Catch;
    }

    private void PlaceLimbAtHitZ(int index, ref RaycastHit hitInfo, Transform holdHandle, float statusType)
    {
        if (holdHandle)
        {
            Limbs[index].position = holdHandle.position;
            Limbs[index].rotation = holdHandle.rotation * handleToLegRot;
        }
        else
        {
            Limbs[index].position = hitInfo.point;
            Limbs[index].rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
        }
        limbStatus[index] = statusType;
    }
    private static Quaternion handleToLegRot = Quaternion.FromToRotation(Vector3.up, Vector3.forward);


    private bool TrySelectFreeLeg(out int index) => TrySelectFreeLimb(out index, 0, 1);
    private bool TrySelectFreeArm(out int index) => TrySelectFreeLimb(out index, 2, 3);

    private bool TrySelectFreeLimb(out int index, int i1, int i2)
    {
        if (limbStatus[i1] <= Free && limbStatus[i1] <= limbStatus[i2])
        {
            index = i1;
            return true;
        }
        else if (limbStatus[i2] <= Free)
        {
            index = i2;
            return true;
        }
        index = -1;
        return false;
    }

    private void TryCatchArm(int index)
    {
        // otherPlaced a center se zachyti PRED pokusy - uspesny attach muze stav koncetin zmenit.
        bool otherPlaced = ArmCatched;
        var center = ArmSphere.transform.position.XY();

        if (TryCatchNearbyObject(index, otherPlaced, center))
            return;

        CollectCellCornerCandidates();
        TryCatchCellCorner(index, otherPlaced, center);
    }

    // Faze 1: chytit se uz existujiciho Placeable typu Catch v dosahu ruky.
    private bool TryCatchNearbyObject(int index, bool otherPlaced, Vector2 center)
    {
        var center3d = ArmSphere.transform.position + new Vector3(0, 0, Settings.limbZ[index]);

        var radius2 = new Vector2(ArmSphere.radius, ArmSphere.radius);
        map.Get(placeables, center - radius2, 2 * radius2, Ksid.Catch);

        foreach (var p in placeables)
        {
            var pos = p.GetClosestPoint(center3d);
            if (!otherPlaced || Vector2.Dot(desiredVelocity, pos - center3d) >= 0)
            {
                if (Physics.Raycast(center3d, pos - center3d, out var hitInfo, ArmSphere.radius, Settings.armCatchLayerMask, QueryTriggerInteraction.Ignore))
                {
                    if ((hitInfo.point - pos).sqrMagnitude < 0.001 && TryAttachLimbTo(index, ref hitInfo, p))
                    {
                        PlaceLimbAtHitZ(index, ref hitInfo, null, Catch);
                        ApplyLimbImpactDamage(index);
                        placeables.Clear();
                        return true;
                    }
                }
            }
        }

        placeables.Clear();
        return false;
    }

    // Faze 2: nasbirat do statickeho bufru armCandidates rohy blokujicich bunek v dosahu ruky.
    private void CollectCellCornerCandidates()
    {
        var c = map.WorldToCell(ArmSphere.transform.position.XY());
        var c1 = c - Settings.ArmCellRadius;
        var c2 = c + Settings.ArmCellRadius + Vector2Int.one;

        var blocking = CellUtils.Combine(SubCellFlags.Full, transform);

        for (int y = c1.y; y < c2.y; y++)
        {
            for (int x = c1.x; x < c.x; x++)
            {
                if ((map.GetCellBlocking(new Vector2Int(x, y)) & blocking) == blocking
                    && (map.GetCellBlocking(new Vector2Int(x + 1, y)) & blocking) != blocking
                    && (map.GetCellBlocking(new Vector2Int(x + 1, y + 1)) & blocking) != blocking
                    && (map.GetCellBlocking(new Vector2Int(x, y + 1)) & blocking) != blocking)
                {
                    armCandidates.Add(map.CellToWorld(new Vector2Int(x + 1, y + 1)));
                }
            }
            for (int x = c.x + 1; x < c2.x; x++)
            {
                if ((map.GetCellBlocking(new Vector2Int(x, y)) & blocking) == blocking
                    && (map.GetCellBlocking(new Vector2Int(x - 1, y)) & blocking) != blocking
                    && (map.GetCellBlocking(new Vector2Int(x - 1, y + 1)) & blocking) != blocking
                    && (map.GetCellBlocking(new Vector2Int(x, y + 1)) & blocking) != blocking)
                {
                    armCandidates.Add(map.CellToWorld(new Vector2Int(x, y + 1)));
                }
            }
        }
    }

    // Faze 3: zkusit se chytit nasbiranych rohu. Vzdy vyprazdni armCandidates.
    private void TryCatchCellCorner(int index, bool otherPlaced, Vector2 center)
    {
        foreach (var pos in armCandidates)
        {
            if (!otherPlaced || Vector2.Dot(desiredVelocity, pos - center) >= 0)
                if (TryCatchArmAt(index, new Vector3(pos.x, pos.y, ArmSphere.transform.position.z), ArmSphere.radius))
                    break;
        }

        armCandidates.Clear();
    }

    private bool TryCatchArmAt(int index, Vector3 candidate, float radius)
    {
        if (Physics.Raycast(ArmSphere.transform.position, candidate - ArmSphere.transform.position, out var hitInfo, radius, Settings.armCatchLayerMask, QueryTriggerInteraction.Ignore))
        {
            if ((hitInfo.point - candidate).sqrMagnitude < 0.001 && TryAttachLimbTo(index, ref hitInfo))
            {
                PlaceLimbAtOwnZ(index, ref hitInfo);
                ApplyLimbImpactDamage(index);
                return true;
            }
        }
        return false;
    }
}
