﻿using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Core.Inventory;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public abstract class ChLegsArms : MonoBehaviour, IHasCleanup, IHasAfterMapPlaced
{
    private const float Free = 0;
    private const float Timeout = 1;
    private const float Catch = 2;
    private const float Hold = 3;
    private const float PickUp = 4;

    public ChSettings Settings;

    public Transform[] Legs;
    private float[] legArmStatus = new float[4];
    private Label[] legsConnectedLabels = new Label[4];
    private Connectable[] legsConnectables = new Connectable[4];

    protected bool LegOnGround => legArmStatus[0] == Catch || legArmStatus[1] == Catch;
    protected bool ArmCatched => legArmStatus[2] == Catch || legArmStatus[3] == Catch;
    protected bool ArmHolds => legArmStatus[2] == Hold || legArmStatus[3] == Hold;

    private Vector3 legUpDir = Vector3.up;

    public SphereCollider LegSphere;
    public SphereCollider ArmSphere;

    protected Rigidbody body;
    protected Placeable placeable;
    protected Map map;

    protected Vector2 desiredVelocity;
    protected int lastXOrientation = 1;
    protected bool desiredJump;
    protected float desiredZMove;
    protected bool desiredCatch;
    protected bool desiredHold;
    protected bool desiredPickUp;
    protected bool pickupToHold;
    protected bool desiredCrouch;
    protected Vector2 holdTarget;


    private static List<Vector2> armCandidates = new List<Vector2>();
    private static List<Placeable> placeables = new List<Placeable>();

    private Label delayedEnableCollisionLabel;
    private Vector3 mClose;
    private readonly Action<object, int> DelayedEnableCollisionsA;

    public ChLegsArms()
    {
        DelayedEnableCollisionsA = DelayedEnableCollisions;
    }

    protected void AwakeB()
    {
        body = GetComponent<Rigidbody>();
        placeable = GetComponent<Placeable>();
        Settings.Initialize(ArmSphere, Legs);
        InitConnectables();
    }

    private void InitConnectables()
    {
        for (int f = 0; f < Legs.Length; f++)
        {
            int index = f;
            legsConnectables[f] = Legs[f].GetComponent<Connectable>();
            legsConnectables[f].Init(() => RemoveLegArmInner(index));
        }
    }

    protected void AdjustLegsArms(bool allowHoldDrop)
    {
        map.Move(placeable);

        DoTimeouts();

        TryRemoveLeg(0);
        TryRemoveLeg(1);
        TryRemoveArm(2, allowHoldDrop);
        TryRemoveArm(3, allowHoldDrop);

        if (!desiredCrouch && Vector3.Dot(body.linearVelocity, legUpDir) <= 0 && SelectFreeLeg(out var index))
        {
            TryPlaceLeg(index);
        }

        if (desiredCatch && SelectFreeArm(out index))
        {
            TryPlaceArm(index);
        }

        bool tryHold = desiredHold && !ArmHolds;
        bool freeArm = SelectFreeArm(out index);
        if (!freeArm && !tryHold && pickupToHold && !desiredPickUp)
        {
            index = GetHoldIndex();
            freeArm = index != -1;
        }
        if ((tryHold || desiredPickUp || pickupToHold) && freeArm)
        {
            TryHold(index, tryHold);
        }

        RemoveCatchIfHold();
    }


    private void RemoveCatchIfHold()
    {
        for (int f = 0; f < legArmStatus.Length; f++)
        {
            if (legArmStatus[f] == Hold || legArmStatus[f] == PickUp)
            {
                for (int g = 0; g < legArmStatus.Length; g++)
                {
                    if (legArmStatus[g] == Catch && legsConnectedLabels[f] == legsConnectedLabels[g])
                        RemoveLegArm(g);
                }
            }
        }
    }

    private void DoTimeouts()
    {
        for (int f = 0; f < legArmStatus.Length; f++)
        {
            if (legArmStatus[f] <= Timeout)
                legArmStatus[f] -= Time.deltaTime * Settings.LegTimeout;
        }
    }

    private void TryRemoveLeg(int index)
    {
        if (legArmStatus[index] == Catch)
        {
            if (desiredCrouch)
            {
                RemoveLegArm(index);
            }
            else
            {
                var lpos = Legs[index].position.XY();
                var center = LegSphere.transform.position.XY();
                var radius = desiredJump ? LegSphere.radius * 1.2f : LegSphere.radius;
                if ((lpos - center).sqrMagnitude > radius * radius)
                {
                    RemoveLegArm(index);
                }
                else if (legArmStatus[OtherIndex(index)] == Catch)
                {
                    float otherX = Legs[OtherIndex(index)].position.x;
                    if (lpos.x <= otherX && otherX < center.x && body.linearVelocity.x >= 0)
                        RemoveLegArm(index);
                    if (lpos.x >= otherX && otherX > center.x && body.linearVelocity.x <= 0)
                        RemoveLegArm(index);
                }
            }
        }
    }

    private void TryRemoveArm(int index, bool allowHoldDrop)
    {
        if (legArmStatus[index] == Catch)
        {
            if (!desiredCatch)
            {
                RemoveLegArm(index);
            }
            else
            {
                var lpos = Legs[index].position.XY();
                var center = ArmSphere.transform.position.XY();
                var radius = ArmSphere.radius;
                if ((lpos - center).sqrMagnitude > radius * radius)
                {
                    RemoveLegArm(index);
                }
                else if (legArmStatus[OtherIndex(index)] == Catch)
                {
                    var dotPos1 = Vector2.Dot(desiredVelocity, lpos - center);
                    if (dotPos1 < 0)
                    {
                        var dotPos2 = Vector2.Dot(desiredVelocity, Legs[OtherIndex(index)].position.XY() - center);
                        if (dotPos1 <= dotPos2)
                            RemoveLegArm(index);
                    }
                }
            }
        }
        else if (legArmStatus[index] == Hold)
        {
            if (!desiredHold)
            {
                EnableCollision(legsConnectedLabels[index]);
                RemoveLegArm(index);
            }
            else if (allowHoldDrop)
            {
                var lpos = Legs[index].position.XY();
                var center = ArmSphere.transform.position.XY();
                var radius = ArmSphere.radius * 1.8f;
                if ((lpos - center).sqrMagnitude > radius * radius)
                {
                    EnableCollision(legsConnectedLabels[index]);
                    RemoveLegArm(index);
                }
            }
        }
        else if (legArmStatus[index] == PickUp)
        {
            var lpos = Legs[index].position.XY();
            var center = ArmSphere.transform.position.XY();
            var radius = ArmSphere.radius * 1.8f;
            var dist = (lpos - center).sqrMagnitude;
            if (dist > radius * radius)
            {
                legArmStatus[index] = Timeout;
                EnableCollision(legsConnectedLabels[index]);
                RemoveLegArm(index);
            }
            var dest = ArmSphere.transform.position.XY() + ComputeHoldTarget(index);
            dist = (lpos - dest).sqrMagnitude;
            if (dist < 0.3f * 0.3f)
            {
                EnableCollision(legsConnectedLabels[index]);
                RemoveLegArm(index);
            }
        }
    }

    protected void RecatchHold()
    {
        if (legArmStatus[2] == Hold)
            RecatchHold(2);
        if (legArmStatus[3] == Hold)
            RecatchHold(3);
    }

    private void RecatchHold(int index)
    {
        EnableCollision(legsConnectedLabels[index]);
        RemoveLegArm(index);
        legArmStatus[index] = Free;
    }

    private int OtherIndex(int index) => index ^ 1;

    private void RemoveLegArm(int index)
    {
        legsConnectables[index].Disconnect();
        if (legsConnectedLabels[index] || legArmStatus[index] > Timeout)
            Debug.LogError("Dosconnect se neudelal");
    }

    private Transform RemoveLegArmInner(int index)
    {
        if (legArmStatus[index] == Hold && IsInventoryActive && !desiredHold)
            InventoryReturn();
        if (legArmStatus[index] == PickUp)
            InventoryPickup(legsConnectedLabels[index]);
        legArmStatus[index] = Timeout;
        legsConnectedLabels[index] = null;
        return transform;
    }

    protected virtual void InventoryPickup(Label label) { }
    protected virtual void InventoryPickupAndActivate(Label label) { }

    private void RemoveAllLegs()
    {
        if (legArmStatus[0] == Catch)
            RemoveLegArm(0);
        if (legArmStatus[1] == Catch)
            RemoveLegArm(1);
    }

    private void RemoveAllCatchedLegsArms()
    {
        for (int f = 0; f < Legs.Length; f++)
        {
            if (legArmStatus[f] == Catch)
                RemoveLegArm(f);
        }
    }

    private void RemoveAllLegsArms()
    {
        for (int f = 0; f < Legs.Length; f++)
        {
            if (legsConnectedLabels[f] != null)
                RemoveLegArm(f);
        }
    }

    private void ActivateSomeLegsArms()
    {
        if (legArmStatus[0] > Free && legArmStatus[1] > Free && !LegOnGround)
        {
            if (legArmStatus[0] < legArmStatus[1] && legArmStatus[0] <= Timeout)
            {
                legArmStatus[0] = Free;
            }
            else if (legArmStatus[1] <= Timeout)
            {
                legArmStatus[1] = Free;
            }
        }

        if (legArmStatus[2] > Free && legArmStatus[3] > Free && !ArmCatched)
        {
            if (legArmStatus[2] < legArmStatus[3] && legArmStatus[2] <= Timeout)
            {
                legArmStatus[2] = Free;
            }
            else if (legArmStatus[3] <= Timeout)
            {
                legArmStatus[3] = Free;
            }
        }
    }

    private void TryPlaceLeg(int index)
    {
        if (legArmStatus[OtherIndex(index)] == Catch)
        {
            float otherX = Legs[OtherIndex(index)].position.x;
            var centerX = LegSphere.transform.position.x;
            if (otherX > centerX && body.linearVelocity.x > 0)
                return;
            if (otherX < centerX && body.linearVelocity.x < 0)
                return;
        }

        Vector3 direction = Vector3.down * Settings.maxSpeed + body.linearVelocity;
        if (RayCastLeg(index, direction, LegSphere.radius))
            return;

        float radius = desiredJump ? LegSphere.radius * 1.2f : LegSphere.radius * 0.7f;
        if (RayCastLeg(index, Vector3.down, radius))
            return;

        if (desiredJump && legArmStatus[OtherIndex(index)] != Catch)
        {
            direction = new Vector3(Mathf.Sign(body.linearVelocity.x) * -0.5f, -1f);
            if (RayCastLeg(index, direction, radius))
                return;
        }
    }

    private bool RayCastLeg(int index, Vector3 direction, float radius)
    {
        if (Physics.Raycast(LegSphere.transform.position, direction, out var hitInfo, radius, Settings.legStandLayerMask, QueryTriggerInteraction.Ignore))
        {
            if (hitInfo.normal.y >= Settings.minGroundDotProduct && ConnectLabel(index, ref hitInfo))
            {
                PlaceLeg(index, ref hitInfo);
                return true;
            }
        }
        return false;
    }

    bool ConnectLabel(int index, ref RaycastHit hitInfo, Label reqLabel = null)
    {
        if (Label.TryFind(hitInfo.collider.transform, out var label) && (reqLabel == null || label == reqLabel))
        {
            DisconnectOppositeAttachements(label);
            legsConnectedLabels[index] = label;
            legsConnectables[index].ConnectTo(label, ConnectableType.LegArm, true);
            return true;
        }
        return false;
    }

    private void DisconnectOppositeAttachements(Label label)
    {
        if (label.KsidGet.IsChildOf(Ksid.DisconnectedByCatch) && label.TryGetComponent<IConnector>(out var connector))
            connector.Disconnect(placeable);
    }

    private void PlaceLeg(int index, ref RaycastHit hitInfo)
    {
        Legs[index].position = new Vector3(hitInfo.point.x, hitInfo.point.y, Settings.legZ[index] + transform.position.z);
        Legs[index].rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
        legArmStatus[index] = Catch;
    }

    private void PlaceLeg3d(int index, ref RaycastHit hitInfo, Transform holdHandle, float statusType)
    {
        if (holdHandle)
        {
            Legs[index].position = holdHandle.position;
            Legs[index].rotation = holdHandle.rotation * handleToLegRot;
        }
        else
        {
            Legs[index].position = hitInfo.point;
            Legs[index].rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
        }
        legArmStatus[index] = statusType;
    }
    private static Quaternion handleToLegRot = Quaternion.FromToRotation(Vector3.up, Vector3.forward);


    private bool SelectFreeLeg(out int index) => SelectFreeLegArm(out index, 0, 1);
    private bool SelectFreeArm(out int index) => SelectFreeLegArm(out index, 2, 3);

    private bool SelectFreeLegArm(out int index, int i1, int i2)
    {
        if (legArmStatus[i1] <= Free && legArmStatus[i1] <= legArmStatus[i2])
        {
            index = i1;
            return true;
        }
        else if (legArmStatus[i2] <= Free)
        {
            index = i2;
            return true;
        }
        index = -1;
        return false;
    }

    private void TryPlaceArm(int index)
    {
        bool otherPlaced = ArmCatched;
        var center = ArmSphere.transform.position.XY();
        var center3d = ArmSphere.transform.position + new Vector3(0, 0, Settings.legZ[index]);

        var radius2 = new Vector2(ArmSphere.radius, ArmSphere.radius);
        map.Get(placeables, center - radius2, 2 * radius2, Ksid.Catch);

        foreach (var p in placeables)
        {
            var pos = p.GetClosestPoint(center3d);
            if (!otherPlaced || Vector2.Dot(desiredVelocity, pos - center3d) >= 0)
            {
                if (Physics.Raycast(center3d, pos - center3d, out var hitInfo, ArmSphere.radius, Settings.armCatchLayerMask, QueryTriggerInteraction.Ignore))
                {
                    if ((hitInfo.point - pos).sqrMagnitude < 0.001 && ConnectLabel(index, ref hitInfo, p))
                    {
                        PlaceLeg3d(index, ref hitInfo, null, Catch);
                        placeables.Clear();
                        return;
                    }
                }
            }
        }

        placeables.Clear();

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

        foreach (var pos in armCandidates)
        {
            if (!otherPlaced || Vector2.Dot(desiredVelocity, pos - center) >= 0)
                if (RayCastArm(index, new Vector3(pos.x, pos.y, ArmSphere.transform.position.z), ArmSphere.radius))
                    break;
        }

        armCandidates.Clear();
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
        var center3d = ArmSphere.transform.position + new Vector3(0, 0, Settings.legZ[index]);
        var radius2 = new Vector2(ArmSphere.radius, ArmSphere.radius) * 1.2f;
        map.Get(placeables, center - radius2 * 1.4f, 2.8f * radius2, Settings.HoldType);

        if (delayedEnableCollisionLabel != null)
        {
            foreach (var p in placeables)
            {
                if (p == delayedEnableCollisionLabel)
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
        var center3d = ArmSphere.transform.position + new Vector3(0, 0, Settings.legZ[index]);
        var radius2 = new Vector2(ArmSphere.radius, ArmSphere.radius) * 1.2f;
        map.Get(placeables, center - radius2 * 1.4f, 2.8f * radius2, Settings.HoldType);

        foreach (var p in placeables)
        {
            if (TryHoldOne(p, index, center3d, tryPickUp: true, tryHold) == HoldOneResult.Ok)
                break;
        }

        placeables.Clear();
    }

    private enum HoldOneResult
    {
        Ok,
        Failed,
        FailedToHit,
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
            var radius = p == delayedEnableCollisionLabel
                ? ArmSphere.radius * 1.5f
                : Mathf.Sqrt(zDiff * zDiff + ArmSphere.radius * ArmSphere.radius * 1.4f * 1.4f);
            if (holdsAtHandle)
                radius *= 1.2f * 1.2f;

            bool pickUpAllowed = false;
            if (tryPickUp)
            {
                pickUpAllowed = IsPickupAllowed(p);
                if (!tryHold && !pickUpAllowed)
                    return HoldOneResult.Failed;
                Vector3 mousePos = GetPickupMousePos(p.transform.position.z);
                var mClose = p.GetClosestPoint(mousePos);
                if ((mousePos - mClose).sqrMagnitude > 0.1f * 0.1f)
                    return HoldOneResult.Failed;
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
                            RecatchHold();
                            desiredHold = true;
                        }
                        if (tryHold && IsInventoryActive && InventoryGet() != p)
                            InventoryReturn();

                        if (ConnectLabel(index, ref hitInfo, p))
                        {
                            pickupToHold = false;
                            if (!p.HasActiveRB)
                                ((Placeable)p).AttachRigidBody(true, false);
                            PlaceLeg3d(index, ref hitInfo, holdHandle, tryHold ? Hold : PickUp);
                            IgnoreCollision(legsConnectedLabels[index], true);
                            if (tryHold && pickUpAllowed)
                                InventoryPickupAndActivate(p);
                            if (tryHold)
                                SetHoldTarget(index);
                            TryCorrectZPos(legsConnectedLabels[index]);
                            return HoldOneResult.Ok;
                        }
                    }
                }
                return HoldOneResult.FailedToHit;
            }
        }
        return HoldOneResult.Failed;
    }

    protected virtual Vector3 GetPickupMousePos(float z) => throw new NotSupportedException();
    protected virtual bool IsPickupAllowed(Label p) => false;
    protected virtual bool HasMouseControler => false;

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
        var center3d = ArmSphere.transform.position + new Vector3(0, 0, Settings.legZ[index]);
        if (TryHoldOne(item, index, center3d, tryPickUp: false, tryHold: true) == HoldOneResult.Failed)
            InventoryReturn();
    }

    //private void EnsurePrevioslyHoldIsFirst()
    //{
    //	if (delayedEnableCollisionLabel != null)
    //	{
    //		for (int f = 0; f < placeables.Count; f++)
    //		{
    //			if (placeables[f] == delayedEnableCollisionLabel)
    //			{
    //				var p = placeables[f];
    //				placeables[f] = placeables[0];
    //				placeables[0] = p;
    //				break;
    //			}
    //		}
    //	}
    //}

    private void SetHoldTarget(int index)
    {
        if (holdTarget == Vector2.zero)
            holdTarget = ComputeHoldTarget(index);
    }

    private Vector2 ComputeHoldTarget(int index)
    {
        if (ArmSphere.transform.position.x < Legs[index].position.x)
        {
            return Settings.HoldPosition;
        }
        else
        {
            return new Vector2(-Settings.HoldPosition.x, Settings.HoldPosition.y);
        }
    }

    private void EnableCollision(Label other)
    {
        if (other == delayedEnableCollisionLabel)
            return;
        if (delayedEnableCollisionLabel != null)
        {
            IgnoreCollision(delayedEnableCollisionLabel, false);
        }
        delayedEnableCollisionLabel = other;
        Game.Instance.Timer.Plan(DelayedEnableCollisionsA, 0.25f, other, 0);
    }

    private void IgnoreCollision(Label other, bool ignore)
    {
        if (ignore && other == delayedEnableCollisionLabel)
            delayedEnableCollisionLabel = null;

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

    private void DelayedEnableCollisions(object other, int token)
    {
        var label = (Label)other;
        if (delayedEnableCollisionLabel == label)
        {
            delayedEnableCollisionLabel = null;
            IgnoreCollision(label, false);
        }
    }

    private bool RayCastArm(int index, Vector3 candidate, float radius)
    {
        if (Physics.Raycast(ArmSphere.transform.position, candidate - ArmSphere.transform.position, out var hitInfo, radius, Settings.armCatchLayerMask, QueryTriggerInteraction.Ignore))
        {
            if ((hitInfo.point - candidate).sqrMagnitude < 0.001 && ConnectLabel(index, ref hitInfo))
            {
                PlaceLeg(index, ref hitInfo);
                return true;
            }
        }
        //else if ((ArmSphere.transform.position - candidate).sqrMagnitude <= radius * radius)
        //{
        //	PlaceLeg(index, candidate, (candidate - ArmSphere.transform.position).normalized);
        //	return true;
        //}
        return false;
    }


    public virtual void GameFixedUpdate()
    {
        AdjustXOrientation();

        Vector2 groundVelocity = GetGroundVelocity();
        if (ArmCatched)
        {
            var force = Vector2.ClampMagnitude(groundVelocity + desiredVelocity - body.linearVelocity.XY(), Settings.maxAcceleration);
            body.AddForce(force, ForceMode.VelocityChange);
            SendOppositeForceToLegArms(force * 0.8f);
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
            SendOppositeForceToLegArms(forceVec * 0.8f);
        }

        body.AddForce(GetArmsCatchForce(groundVelocity), ForceMode.VelocityChange);

        bool jumpStarted = false;
        if (desiredJump)
        {
            if (LegOnGround)
            {
                var jumpForce = Mathf.Sqrt(-2f * Physics.gravity.y * Settings.jumpHeight) - body.linearVelocity.y;
                body.AddForce(0, jumpForce, 0, ForceMode.VelocityChange);
                SendOppositeForceToLegs(new Vector3(0, jumpForce, 0), true);
                desiredJump = false;
                jumpStarted = true;
                RemoveAllLegs();
            }
            else
            {
                //body.AddForce(0, -maxAcceleration, 0, ForceMode.VelocityChange);
            }
        }
        else
        {
            Vector3 legF = legUpDir * Mathf.Max(GetLegForce(0), GetLegForce(1));
            body.AddForce(legF, ForceMode.VelocityChange);
            SendOppositeForceToLegs(legF, null);
            float legSF = GetLegSideForce();
            body.AddForce(legSF, 0, 0, ForceMode.VelocityChange);
        }

        if (!jumpStarted)
            body.AddForce(GetDrag());

        if (desiredZMove != 0)
        {
            var p = transform.position;
            if (placeable.CanZMove(p.z + desiredZMove))
            {
                p.z += desiredZMove;
                transform.position = p;
                var ho = GetHoldObject();
                if (ho != null)
                    TryCorrectZPos(ho);
                RemoveAllCatchedLegsArms();
                ActivateSomeLegsArms();
                RecatchHold();
            }
            desiredZMove = 0;
        }

        ApplyHoldForce();
    }

    protected virtual void AdjustXOrientation()
    {
        if (body.linearVelocity.x > Settings.maxSpeed * 0.1f)
            lastXOrientation = 1;
        else if (body.linearVelocity.x < -Settings.maxSpeed * 0.1f)
            lastXOrientation = -1;
    }

    private void ApplyHoldForce()
    {
        if (legArmStatus[2] == Hold)
            ApplyHoldForce(2, false);
        if (legArmStatus[3] == Hold)
            ApplyHoldForce(3, false);
        if (legArmStatus[2] == PickUp)
            ApplyHoldForce(2, true);
        if (legArmStatus[3] == PickUp)
            ApplyHoldForce(3, true);
    }

    private void ApplyHoldForce(int index, bool isPickUp)
    {
        var label = legsConnectedLabels[index];
        if (label != null)
        {
            var lRB = label.Rigidbody;
            if (lRB != null)
            {
                var armPos = Legs[index].position.XY() + label.Velocity.XY() * Time.fixedDeltaTime;
                var destPos = ArmSphere.transform.position.XY();
                var holdTarget = isPickUp ? ComputeHoldTarget(index) : this.holdTarget;
                destPos += holdTarget;
                GetDecollisionDistance(legsConnectedLabels[index], out var decollision);
                destPos += decollision;
                destPos += this.body.linearVelocity.XY() * Time.fixedDeltaTime;

                var center = ArmSphere.transform.position.XY() + this.body.linearVelocity.XY() * Time.fixedDeltaTime;
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
        }
    }

    private void ApplyHoldTorque(int index, Rigidbody lRB, Label p)
    {
        if (!HasMouseControler || !p.KsidGet.IsChildOf(Ksid.HoldsAtHandle))
        {
            lRB.angularVelocity = Vector3.zero;
        }
        else
        {
            Vector3 mousePos = GetPickupMousePos(Legs[index].position.z);
            Vector3 toMouse = mousePos - Legs[index].position;
            if (toMouse.sqrMagnitude > 0.01f)
            {
                var rotVel = lRB.angularVelocity.z;
                var frameRot = rotVel * Time.fixedDeltaTime * Mathf.Rad2Deg;
                var labelRot = Quaternion.Euler(0, 0, frameRot) * Legs[index].rotation;

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


    private Vector2 GetGroundVelocity()
    {
        int count = 0;
        Vector2 res = Vector2.zero;
        for (int f = 0; f < Legs.Length; f++)
        {
            if (legArmStatus[f] == Catch)
            {
                count++;
                res += legsConnectedLabels[f].Velocity.XY();
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
        if (legArmStatus[index] == Catch)
        {
            var legDir = LegSphere.transform.position.XY() - Legs[index].position.XY();
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
        if (legArmStatus[index] == Catch)
        {
            var delta = (LegSphere.transform.position.x - Legs[index].position.x) / LegSphere.radius;
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
        if (legArmStatus[index] == Catch)
        {
            var armDir = (ArmSphere.transform.position.XY() - Legs[index].position.XY());
            var armDirNorm = armDir.normalized;
            var upModifier = Mathf.Clamp01(Vector2.Dot(Vector2.up, desiredVelocity)) * Mathf.Clamp01(Vector2.Dot(Vector2.up, armDirNorm));
            var holdVel = Vector2.Dot(armDirNorm, forceToReduce) + 1 - upModifier;
            var inForce = armDir.sqrMagnitude / (ArmSphere.radius * ArmSphere.radius);
            inForce = Mathf.Max(0, inForce - 0.7f * 0.7f);
            inForce *= Settings.ArmInForceCoef * holdVel;
            var armForce = -inForce * armDirNorm;
            result += armForce;
            forceToReduce += armForce;

            var relVelocity = Vector2.Dot(armDirNorm, legsConnectedLabels[index].Velocity.XY() - myVelocity);
            bool isImpact = relVelocity * relVelocity > PhysicsConsts.ImpactVelocitySqr;
            SendOppositeForce(armForce, index, isImpact);
        }
    }

    private void SendOppositeForceToLegArms(Vector3 velocity)
    {
        int count = 0;
        foreach (var status in legArmStatus)
            if (status == Catch)
                count++;

        if (count > 0)
        {
            for (int f = 0; f < legArmStatus.Length; f++)
            {
                if (legArmStatus[f] == Catch)
                    SendOppositeForce(velocity / count, f, null);
            }
        }
    }


    private void SendOppositeForceToLegs(Vector3 velocity, bool? isImpact)
    {
        if (legArmStatus[0] == Catch)
        {
            if (legArmStatus[1] == Catch)
            {
                SendOppositeForce(velocity * 0.5f, 0, isImpact);
                SendOppositeForce(velocity * 0.5f, 1, isImpact);
            }
            else
            {
                SendOppositeForce(velocity, 0, isImpact);
            }
        }
        else if (legArmStatus[1] == Catch)
        {
            SendOppositeForce(velocity, 1, isImpact);
        }
    }

    private void SendOppositeForce(Vector3 vector3, int index, bool? isImpact)
    {
        if (isImpact == null)
        {
            var relVelocity = body.linearVelocity.XY() - legsConnectedLabels[index].Velocity.XY();
            isImpact = relVelocity.sqrMagnitude > PhysicsConsts.ImpactVelocitySqr;
        }
        legsConnectedLabels[index].ApplyVelocity(-vector3, body.mass, isImpact.Value ? VelocityFlags.IsImpact : VelocityFlags.None);
    }

    public Label GetHoldObject()
    {
        if (legArmStatus[2] == Hold && legsConnectedLabels[2] && legsConnectedLabels[2].HasActiveRB)
            return legsConnectedLabels[2];
        if (legArmStatus[3] == Hold && legsConnectedLabels[3] && legsConnectedLabels[3].HasActiveRB)
            return legsConnectedLabels[3];
        return null;
    }

    public Transform GetHoldLeg()
    {
        if (legArmStatus[2] == Hold && legsConnectedLabels[2])
            return Legs[2];
        if (legArmStatus[3] == Hold && legsConnectedLabels[3])
            return Legs[3];
        return null;
    }

    private int GetHoldIndex()
    {
        if (legArmStatus[2] == Hold)
            return 2;
        if (legArmStatus[3] == Hold)
            return 3;
        return -1;
    }

    public virtual void Cleanup(bool goesToInventory)
    {
        Debug.Assert(!goesToInventory, "Nepodporuju imistovani do inventare");
        RemoveAllLegsArms();
        map = null;
    }

    public virtual void AfterMapPlaced(Map map, Placeable placeableSibling, bool goesFromInventory)
    {
        this.map = map;
    }


    public virtual Label InventoryGet() => null;
    public virtual bool IsInventoryActive => false;
    public virtual void InventoryReturn() => throw new NotSupportedException();
    public virtual void InventoryDrop() => throw new NotSupportedException();

    public Map ActiveMap => map;
}

