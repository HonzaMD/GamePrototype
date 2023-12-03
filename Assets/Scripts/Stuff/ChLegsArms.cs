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
using UnityEditor;
using UnityEngine;

public abstract class ChLegsArms : MonoBehaviour, IHasCleanup
{
	private const float Free = 0;
	private const float Timeout = 1;
	private const float Catch = 2;
	private const float Hold = 3;

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
	
	protected Vector2 desiredVelocity;
	protected int lastXOrientation = 1;
	protected bool desiredJump;
	protected float desiredZMove;
	protected bool desiredCatch;
	protected bool desiredHold;
	protected bool desiredCrouch;
	protected Vector2 holdTarget;
	protected IInventoryAccessor inventoryAccessor;


	private static List<Vector2> armCandidates = new List<Vector2>();
	private static List<Placeable> placeables = new List<Placeable>();

	private Label delayedEnableCollisionLabel;
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

	protected void AdjustLegsArms()
	{
		Game.Map.Move(placeable);

		DoTimeouts();

		TryRemoveLeg(0);
		TryRemoveLeg(1);
		TryRemoveArm(2);
		TryRemoveArm(3);

		if (!desiredCrouch && Vector3.Dot(body.velocity, legUpDir) <= 0 && SelectFreeLeg(out var index))
		{
			TryPlaceLeg(index);
		}

		if (desiredCatch && SelectFreeArm(out index))
		{
			TryPlaceArm(index);
		}

		if (desiredHold && !ArmHolds && SelectFreeArm(out index))
		{
			TryHold(index);
		}

		RemoveCatchIfHold();
	}

	private void RemoveCatchIfHold()
	{
		for (int f = 0; f < legArmStatus.Length; f++)
		{
			if (legArmStatus[f] == Hold)
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
					if (lpos.x <= otherX && otherX < center.x && body.velocity.x >= 0)
						RemoveLegArm(index);
					if (lpos.x >= otherX && otherX > center.x && body.velocity.x <= 0)
						RemoveLegArm(index);
				}
			}
		}
	}

	private void TryRemoveArm(int index)
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
			else
			{
				var lpos = Legs[index].position.XY();
				var center = ArmSphere.transform.position.XY();
				var radius = ArmSphere.radius * 1.5f;
				if ((lpos - center).sqrMagnitude > radius * radius)
				{
					EnableCollision(legsConnectedLabels[index]);
					RemoveLegArm(index);
				}
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
        if (legArmStatus[index] == Hold && inventoryAccessor != null)
            inventoryAccessor.InventoryReturn(legsConnectedLabels[index]);
        legArmStatus[index] = Timeout;
        legsConnectedLabels[index] = null;
		return transform;
    }

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
			if (otherX > centerX && body.velocity.x > 0)
				return;
			if (otherX < centerX && body.velocity.x < 0)
				return;
		}

		Vector3 direction = Vector3.down * Settings.maxSpeed + body.velocity;
		if (RayCastLeg(index, direction, LegSphere.radius))
			return;

		float radius = desiredJump ? LegSphere.radius * 1.2f : LegSphere.radius * 0.7f;
		if (RayCastLeg(index, Vector3.down, radius))
			return;

		if (desiredJump && legArmStatus[OtherIndex(index)] != Catch)
		{
			direction = new Vector3(Mathf.Sign(body.velocity.x) * -0.5f, -1f);
			if (RayCastLeg(index, direction, radius))
				return;
		}
	}

	private bool RayCastLeg(int index, Vector3 direction, float radius)
	{
		if (Physics.Raycast(LegSphere.transform.position, direction, out var hitInfo, radius, Settings.legStandLayerMask))
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

	private void PlaceLeg3d(int index, ref RaycastHit hitInfo, float statusType)
	{
		Legs[index].position = hitInfo.point;
		Legs[index].rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
		legArmStatus[index] = statusType;
	}


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
		var map = Game.Map;
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
				if (Physics.Raycast(center3d, pos - center3d, out var hitInfo, ArmSphere.radius, Settings.armCatchLayerMask))
				{
					if ((hitInfo.point - pos).sqrMagnitude < 0.001 && ConnectLabel(index, ref hitInfo, p))
					{
						PlaceLeg3d(index, ref hitInfo, Catch);
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

	private void TryHold(int index)
	{
		if (inventoryAccessor != null)
		{
			TryHoldInventory(index);
		}
		else
		{
			TryHoldNearItem(index);
		}
	}

	private void TryHoldNearItem(int index)
	{
		var map = Game.Map;
		var center = ArmSphere.transform.position.XY();
		var center3d = ArmSphere.transform.position + new Vector3(0, 0, Settings.legZ[index]);
		var radius2 = new Vector2(ArmSphere.radius, ArmSphere.radius) * 1.2f;
		map.Get(placeables, center - radius2, 2 * radius2, Settings.HoldType);

		EnsurePrevioslyHoldIsFirst();

		foreach (var p in placeables)
		{
			if (TryHoldOne(p, index, center3d))
				break;
		}

		placeables.Clear();
	}

	private bool TryHoldOne(Label p, int index, Vector3 center3d)
	{
		if (p.HasActiveRB || p.KsidGet.IsChildOf(Ksid.SandLike))
		{
			var pos = p.GetClosestPoint(center3d);
			var zDiff = center3d.z - pos.z;
			var radius = p == delayedEnableCollisionLabel
				? ArmSphere.radius * 1.5f
                : Mathf.Sqrt(zDiff * zDiff + ArmSphere.radius * ArmSphere.radius);
			if (Physics.Raycast(center3d, pos - center3d, out var hitInfo, radius, Settings.armCatchLayerMask))
			{
				if ((hitInfo.point - pos).sqrMagnitude < 0.001 && ConnectLabel(index, ref hitInfo, p))
				{
					if (!p.HasActiveRB)
						((Placeable)p).AttachRigidBody(true, false);
					PlaceLeg3d(index, ref hitInfo, Hold);
					IgnoreCollision(legsConnectedLabels[index], true);
					SetHoldTarget(index);
					TryCorrectZPos(legsConnectedLabels[index]);
					return true;
				}
			}
		}
		return false;
	}

	private void TryCorrectZPos(Label label)
	{
		var p = label.PlaceableC;
		int myCellZ = placeable.CellZ;
		if (!p.CellBlocking.IsDoubleCell() && p.CellZ != myCellZ)
		{
			if (p.CanZMove(myCellZ * Map.CellSize.z, placeable))
				p.MoveZ(myCellZ * Map.CellSize.z);
		}
	}

	private void TryHoldInventory(int index)
	{
		var item = inventoryAccessor.InventoryGet();
		var center3d = ArmSphere.transform.position + new Vector3(0, 0, Settings.legZ[index]);
		if (!TryHoldOne(item, index, center3d))
			inventoryAccessor.InventoryReturn(item);
	}

	private void EnsurePrevioslyHoldIsFirst()
	{
		if (delayedEnableCollisionLabel != null)
		{
			for (int f = 0; f < placeables.Count; f++)
			{
				if (placeables[f] == delayedEnableCollisionLabel)
				{
					var p = placeables[f];
					placeables[f] = placeables[0];
					placeables[0] = p;
					break;
				}
			}
		}
	}

	private void SetHoldTarget(int index)
	{
		if (holdTarget == Vector2.zero)
		{
			if (ArmSphere.transform.position.x < Legs[index].position.x)
			{
				holdTarget += Settings.HoldPosition;
			}
			else
			{
				holdTarget += new Vector2(-Settings.HoldPosition.x, Settings.HoldPosition.y);
			}
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
		if (Physics.Raycast(ArmSphere.transform.position, candidate - ArmSphere.transform.position, out var hitInfo, radius, Settings.armCatchLayerMask))
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
		if (body.velocity.x > Settings.maxSpeed * 0.1f)
			lastXOrientation = 1;
		else if (body.velocity.x < -Settings.maxSpeed * 0.1f)
			lastXOrientation = -1;

		Vector2 groundVelocity = GetGroundVelocity();
		if (ArmCatched)
		{
			var force = Vector2.ClampMagnitude(groundVelocity + desiredVelocity - body.velocity.XY(), Settings.maxAcceleration);
			body.AddForce(force, ForceMode.VelocityChange);
			SendOppositeForceToLegArms(force * 0.8f);
            legUpDir = Vector3.up;
		}
		else
		{
			Quaternion legRot = GetLegRotation();
			var xAxis = legRot * Vector3.right;
			legUpDir = legRot * Vector3.up;
			var xVel = Vector3.Dot(xAxis, body.velocity);
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
				var jumpForce = Mathf.Sqrt(-2f * Physics.gravity.y * Settings.jumpHeight) - body.velocity.y;
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

	private void ApplyHoldForce()
	{
		if (legArmStatus[2] == Hold)
			ApplyHoldForce(2);
		if (legArmStatus[3] == Hold)
			ApplyHoldForce(3);
	}

	private void ApplyHoldForce(int index)
	{
		var label = legsConnectedLabels[index];
		if (label != null) 
		{
            var lRB = label.Rigidbody;
            if (lRB != null)
            {
                var armPos = Legs[index].position.XY() + label.Velocity.XY() * Time.fixedDeltaTime;
				var destPos = ArmSphere.transform.position.XY() + holdTarget;
				destPos += this.body.velocity.XY() * Time.fixedDeltaTime;
				GetDecollisionDistance(legsConnectedLabels[index], out var decollision);
				destPos += decollision;

				var dist = (destPos - armPos) * Settings.HoldMoveSpeed * Settings.HoldMoveSpeed;
				var koef = body.mass * 0.6f / lRB.mass;
				if (koef > 1)
					koef = Mathf.Log(koef) + 1;
                var force = Vector2.ClampMagnitude(dist, Settings.HoldMoveAcceleration * koef);
				label.ApplyVelocity(force, body.mass * 0.6f, VelocityFlags.LimitVelocity);

				lRB.angularVelocity = Vector3.zero;
				body.AddForce(-force * 0.8f, lRB.mass, VelocityFlags.None);
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
		if (Physics.Raycast(LegSphere.transform.position, -legUpDir, out var hitInfo, LegSphere.radius * 1.3f, Settings.legStandLayerMask))
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
		return body.velocity * -body.velocity.magnitude * Settings.DragCoef;
	}

	private float GetLegForce(int index)
	{
		if (legArmStatus[index] == Catch)
		{
			var legDir = LegSphere.transform.position.XY() - Legs[index].position.XY();
			float force = (LegSphere.radius - legDir.magnitude) / LegSphere.radius;
			force *= Settings.LegForce;
			force += Vector3.Dot(legUpDir, body.velocity) * -Settings.LegForceDampening;
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
		var velocity = body.velocity.XY();
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
            var relVelocity = body.velocity.XY() - legsConnectedLabels[index].Velocity.XY();
            isImpact = relVelocity.sqrMagnitude > PhysicsConsts.ImpactVelocitySqr;
        }
        legsConnectedLabels[index].ApplyVelocity(-vector3, body.mass, isImpact.Value ? VelocityFlags.IsImpact : VelocityFlags.None);
	}

	protected Label GetHoldObject()
	{
		if (legArmStatus[2] == Hold && legsConnectedLabels[2] && legsConnectedLabels[2].HasActiveRB)
			return legsConnectedLabels[2];
		if (legArmStatus[3] == Hold && legsConnectedLabels[3] && legsConnectedLabels[3].HasActiveRB)
			return legsConnectedLabels[3];
		return null;
	}

	public void Cleanup()
	{
		RemoveAllLegsArms();
		body.Cleanup();
	}
}

