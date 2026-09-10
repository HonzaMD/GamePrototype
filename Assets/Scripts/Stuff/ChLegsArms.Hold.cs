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

// Drzeni a sbirani predmetu: vyber predmetu, uchopeni, inventar, kolize, sila drzeni.
public abstract partial class ChLegsArms
{

    private void TryHoldOrPickUp()
    {
        if (TrySelectArmForHold(out var index, out bool tryHold))
        {
            TryHold(index, tryHold);
        }
    }

    // Tri rezimy se prekryvaji: desiredHold (chci nest), desiredPickUp (chci sebrat do inventare),
    // pickupToHold (sebrani zahajene mysi, ma skoncit v ruce).
    // Kdyz zadna ruka neni volna a jde jen o dokonceni pickupToHold, pouzije se uz DRZICI ruka
    // - predmet v ni se prehmatne na sebrani, misto aby se pickup zahodil.
    private bool TrySelectArmForHold(out int index, out bool tryHold)
    {
        tryHold = desiredHold && !ArmHolds;
        bool freeArm = TrySelectFreeArm(out index);
        if (!freeArm && !tryHold && pickupToHold && !desiredPickUp)
        {
            index = GetHoldIndex();
            freeArm = index != -1;
        }
        return (tryHold || desiredPickUp || pickupToHold) && freeArm;
    }


    private void DetachCatchDuplicatingHold()
    {
        for (int f = 0; f < limbStatus.Length; f++)
        {
            if (limbStatus[f] == Hold || limbStatus[f] == PickUp)
            {
                for (int g = 0; g < limbStatus.Length; g++)
                {
                    if (limbStatus[g] == Catch && limbTargets[f] == limbTargets[g])
                        DetachLimb(g);
                }
            }
        }
    }

    // Pusti drzeny predmet a ruku hned uvolni. Pouziva inventar, ktery do te same ruky
    // vzapeti vklada jinou vec - proto Free misto bezneho Timeoutu.
    protected void ResetHold()
    {
        if (limbStatus[2] == Hold)
        {
            DetachHoldLimb(2);
            limbStatus[2] = Free;
        }
        if (limbStatus[3] == Hold)
        {
            DetachHoldLimb(3);
            limbStatus[3] = Free;
        }
    }

    // Pusti drzeny predmet, ruka jde do bezneho Timeoutu. Pouziva hod: musi pustit uz
    // v tomhle fyzikalnim kroku, jinak by ApplyHoldForce stahla hozenou vec zpatky k ruce.
    protected void DetachHold()
    {
        if (limbStatus[2] == Hold)
            DetachHoldLimb(2);
        if (limbStatus[3] == Hold)
            DetachHoldLimb(3);
    }

    private void DetachHoldLimb(int index)
    {
        ScheduleCollisionRestore(limbTargets[index]);
        DetachLimb(index);
    }

    private void TryHold(int index, bool tryHold)
    {
        if (tryHold && IsInventoryActive)
        {
            TryHoldInventory(index);
        }
        else if (desiredPickUp || pickupToHold)
        {
            TryHoldNearItem(index, tryHold || pickupToHold);
        }
        else if (tryHold)
        {
            TryHoldLastItem(index);
        }
    }

    private void TryHoldLastItem(int index)
    {
        var center = ArmSphere.transform.position.XY();
        var center3d = ArmSphere.transform.position + new Vector3(0, 0, Settings.limbZ[index]);
        var radius2 = new Vector2(ArmSphere.radius, ArmSphere.radius) * 1.2f;
        map.Get(placeables, center - radius2 * 1.4f, 2.8f * radius2, Settings.HoldType);

        if (pendingCollisionRestore != null)
        {
            foreach (var p in placeables)
            {
                if (p == pendingCollisionRestore)
                {
                    TryHoldOne(p, index, center3d, tryPickUp: false, tryHold: true);
                    break;
                }
            }
        }

        placeables.Clear();
    }

    private void TryHoldNearItem(int index, bool tryHold)
    {
        var center = ArmSphere.transform.position.XY();
        var center3d = ArmSphere.transform.position + new Vector3(0, 0, Settings.limbZ[index]);
        var radius2 = new Vector2(ArmSphere.radius, ArmSphere.radius) * 1.2f;
        map.Get(placeables, center - radius2 * 1.4f, 2.8f * radius2, Settings.HoldType);

        foreach (var p in placeables)
        {
            if (TryHoldOne(p, index, center3d, tryPickUp: true, tryHold) == HoldOneResult.Attached)
                break;
        }

        placeables.Clear();
    }

    private enum HoldOneResult
    {
        Attached,       // uchopeno
        NotACandidate,  // objekt vubec nepripada v uvahu (mimo dosah, nesplnuje podminky)
        RaycastMissed,  // kandidat byl, ale raycast na nej netrefil (neco stoji v ceste)
    }

    private HoldOneResult TryHoldOne(Label p, int index, Vector3 center3d, bool tryPickUp, bool tryHold)
    {
        if (p.HasActiveRB || p.KsidGet.IsChildOf(Ksid.SandLike))
        {
            bool holdsAtHandle = p.KsidGet.IsChildOf(Ksid.HoldsAtHandle);
            Transform holdHandle = null;
            if (holdsAtHandle)
                holdHandle = p.GetHoldHandle();
            var pos = holdsAtHandle ? holdHandle.position : p.GetClosestPoint(center3d);
            var zDiff = center3d.z - pos.z;
            var radius = p == pendingCollisionRestore
                ? ArmSphere.radius * 1.5f
                : Mathf.Sqrt(zDiff * zDiff + ArmSphere.radius * ArmSphere.radius * 1.4f * 1.4f);
            if (holdsAtHandle)
                radius *= 1.2f * 1.2f;

            bool pickUpAllowed = false;
            if (tryPickUp)
            {
                pickUpAllowed = IsPickupAllowed(p);
                if (!tryHold && !pickUpAllowed)
                    return HoldOneResult.NotACandidate;
                Vector3 mousePos = GetPickupMousePos(p.transform.position.z);
                var mClose = p.GetClosestPoint(mousePos);
                if ((mousePos - mClose).sqrMagnitude > 0.1f * 0.1f)
                    return HoldOneResult.NotACandidate;
            }

            if ((center3d - pos).magnitude <= radius)
            {
                if (Physics.Raycast(center3d, pos - center3d, out var hitInfo, radius, Settings.armCatchLayerMask, QueryTriggerInteraction.Ignore))
                {
                    if (holdsAtHandle || (hitInfo.point - pos).sqrMagnitude < 0.001)
                    {
                        if (tryHold && desiredHold && ArmHolds)
                        {
                            desiredHold = false;
                            ResetHold();
                            desiredHold = true;
                        }
                        if (tryHold && IsInventoryActive && InventoryGet() != p)
                            InventoryReturn();

                        if (TryAttachLimbTo(index, ref hitInfo, p))
                        {
                            CompleteHold(index, ref hitInfo, holdHandle, p, tryHold, pickUpAllowed);
                            return HoldOneResult.Attached;
                        }
                    }
                }
                return HoldOneResult.RaycastMissed;
            }
        }
        return HoldOneResult.NotACandidate;
    }

    // Vola se az po uspesnem TryAttachLimbTo - vsechny side efekty uchopeni na jednom miste:
    // predmet dostane RB, vypnou se kolize s nami, pripadne se zaeviduje do inventare a srovna Z.
    private void CompleteHold(int index, ref RaycastHit hitInfo, Transform holdHandle, Label p, bool tryHold, bool pickUpAllowed)
    {
        pickupToHold = false;
        if (!p.HasActiveRB)
            ((Placeable)p).AttachRigidBody(true, false);
        PlaceLimbAtHitZ(index, ref hitInfo, holdHandle, tryHold ? Hold : PickUp);
        ApplyLimbImpactDamage(index);
        SetCollisionIgnored(limbTargets[index], true);
        if (tryHold && pickUpAllowed)
            InventoryPickupAndActivate(p);
        if (tryHold)
            SetHoldTarget(index);
        TryCorrectZPos(limbTargets[index]);
    }

    private void TryCorrectZPos(Label label)
    {
        var p = label.PlaceableC;
        int myCellZ = placeable.CellZ;
        if (!p.CellBlocking.IsDoubleCell() && p.CellZ != myCellZ)
        {
            if (p.CanZMove(myCellZ * Map.CellSize.z, placeable))
                p.MoveZ(myCellZ * Map.CellSize.z, map);
        }
    }

    private void TryHoldInventory(int index)
    {
        var item = InventoryGet();
        var center3d = ArmSphere.transform.position + new Vector3(0, 0, Settings.limbZ[index]);
        if (TryHoldOne(item, index, center3d, tryPickUp: false, tryHold: true) == HoldOneResult.NotACandidate)
            InventoryReturn();
    }

    private void SetHoldTarget(int index)
    {
        if (holdTarget == Vector2.zero)
            holdTarget = ComputeHoldTarget(index);
    }

    private Vector2 ComputeHoldTarget(int index)
    {
        if (ArmSphere.transform.position.x < Limbs[index].position.x)
        {
            return Settings.HoldPosition;
        }
        else
        {
            return new Vector2(-Settings.HoldPosition.x, Settings.HoldPosition.y);
        }
    }

    private void ScheduleCollisionRestore(Label other)
    {
        if (other == pendingCollisionRestore)
            return;
        if (pendingCollisionRestore != null)
        {
            SetCollisionIgnored(pendingCollisionRestore, false);
        }
        pendingCollisionRestore = other;
        Game.Instance.Timer.Plan(OnCollisionRestoreTimerA, 0.25f, other, 0);
    }

    private void SetCollisionIgnored(Label other, bool ignore)
    {
        if (ignore && other == pendingCollisionRestore)
            pendingCollisionRestore = null;

        if (!placeable || !other)
            return;

        var colliders1 = placeable.GetCollidersBuff1();
        var colliders2 = other.GetCollidersBuff2();

        foreach (var c1 in colliders1)
            if (c1.enabled)
                foreach (var c2 in colliders2)
                    if (c2.enabled)
                        Physics.IgnoreCollision(c2, c1, ignore);

        colliders1.Clear();
        colliders2.Clear();
    }

    private void OnCollisionRestoreTimer(object other, int token)
    {
        var label = (Label)other;
        if (pendingCollisionRestore == label)
        {
            pendingCollisionRestore = null;
            SetCollisionIgnored(label, false);
        }
    }

    private void ApplyHoldForces()
    {
        if (limbStatus[2] == Hold)
            ApplyHoldForce(2, false);
        if (limbStatus[3] == Hold)
            ApplyHoldForce(3, false);
        if (limbStatus[2] == PickUp)
            ApplyHoldForce(2, true);
        if (limbStatus[3] == PickUp)
            ApplyHoldForce(3, true);
    }

    private void ApplyHoldForce(int index, bool isPickUp)
    {
        var label = limbTargets[index];
        if (label == null)
            return;
        var lRB = label.Rigidbody;
        if (lRB == null)
            return;

        var armPos = Limbs[index].position.XY() + label.Velocity.XY() * Time.fixedDeltaTime;
        var destPos = ComputeHoldDestination(index, isPickUp);

        var center = ArmSphere.transform.position.XY() + body.linearVelocity.XY() * Time.fixedDeltaTime;
        var speed = Mathf.Clamp((center - armPos).magnitude / ArmSphere.radius, 0.5f, 1.3f);

        var dist = (destPos - armPos) * Settings.HoldMoveSpeed * Settings.HoldMoveSpeed;
        var koef = body.mass * 0.6f / lRB.mass;
        if (koef > 1)
            koef = Mathf.Log(koef) + 1;
        var force = Vector2.ClampMagnitude(dist, Settings.HoldMoveAcceleration * speed * koef);
        label.ApplyVelocity(force, body.mass * 0.6f, VelocityFlags.LimitVelocity);

        ApplyHoldTorque(index, lRB, label);

        body.AddForce(-force * 0.8f, lRB.mass, VelocityFlags.None);
    }

    // Kam ma drzeny predmet mirit: pozice ruky + hold target + odstrceni z kolize
    // + predikce posunu tela za jeden fyzikalni krok.
    private Vector2 ComputeHoldDestination(int index, bool isPickUp)
    {
        var destPos = ArmSphere.transform.position.XY();
        destPos += isPickUp ? ComputeHoldTarget(index) : holdTarget;
        GetDecollisionDistance(limbTargets[index], out var decollision);
        destPos += decollision;
        destPos += body.linearVelocity.XY() * Time.fixedDeltaTime;
        return destPos;
    }

    private void ApplyHoldTorque(int index, Rigidbody lRB, Label p)
    {
        if (!HasMouseControler || !p.KsidGet.IsChildOf(Ksid.HoldsAtHandle))
        {
            lRB.angularVelocity = Vector3.zero;
        }
        else
        {
            Vector3 mousePos = GetPickupMousePos(Limbs[index].position.z);
            Vector3 toMouse = mousePos - Limbs[index].position;
            if (toMouse.sqrMagnitude > 0.01f)
            {
                var rotVel = lRB.angularVelocity.z;
                var frameRot = rotVel * Time.fixedDeltaTime * Mathf.Rad2Deg;
                var labelRot = Quaternion.Euler(0, 0, frameRot) * Limbs[index].rotation;

                var mouseRot = Quaternion.FromToRotation(Vector3.down, toMouse);
                var diffRot = labelRot * mouseRot;
                float neededRot = diffRot.eulerAngles.z.Angle180();
                neededRot = Mathf.Clamp(neededRot, -60, 60) * 0.04f;
                neededRot = Mathf.Sign(neededRot) * Mathf.Max(0, neededRot * neededRot - 0.005f);

                var halfRotVel = rotVel * 0.01f;
                var clampedhrv = (halfRotVel > 0) ? Math.Min(halfRotVel, Math.Max(0, neededRot)) : Math.Max(halfRotVel, Math.Min(0, neededRot));
                var dump = clampedhrv - halfRotVel;

                //				Debug.Log($"{neededRot} {rotVel} {dump}");

                lRB.AddTorque(0, 0, neededRot + dump, ForceMode.VelocityChange);
            }
        }
    }

    private void GetDecollisionDistance(Label other, out Vector2 result)
    {
        var colliders1 = placeable.GetCollidersBuff1();
        var colliders2 = other.GetCollidersBuff2();
        result = Vector2.zero;

        foreach (var c1 in colliders1)
        {
            if (c1.enabled)
            {
                foreach (var c2 in colliders2)
                {
                    if (c2.enabled)
                    {
                        if (Physics.ComputePenetration(c1, c1.transform.position, c1.transform.rotation, c2, c2.transform.position, c2.transform.rotation, out var dir, out var dist))
                        {
                            if (dir.x != 0 || dir.y != 0)
                            {
                                result = dir.XY() * -dist;

                                colliders1.Clear();
                                colliders2.Clear();
                                return;
                            }
                        }
                    }
                }
            }
        }

        colliders1.Clear();
        colliders2.Clear();
    }

    public Label GetHoldObject()
    {
        if (limbStatus[2] == Hold && limbTargets[2] && limbTargets[2].HasActiveRB)
            return limbTargets[2];
        if (limbStatus[3] == Hold && limbTargets[3] && limbTargets[3].HasActiveRB)
            return limbTargets[3];
        return null;
    }

    public Transform GetHoldLimb()
    {
        if (limbStatus[2] == Hold && limbTargets[2])
            return Limbs[2];
        if (limbStatus[3] == Hold && limbTargets[3])
            return Limbs[3];
        return null;
    }

    private int GetHoldIndex()
    {
        if (limbStatus[2] == Hold)
            return 2;
        if (limbStatus[3] == Hold)
            return 3;
        return -1;
    }
}
