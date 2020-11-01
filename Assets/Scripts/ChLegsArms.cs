using Assets.Scripts.Core;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public abstract class ChLegsArms : MonoBehaviour
{
	private const float Free = 0;
	private const float Timeout = 1;
	private const float Catch = 2;
	private const float Hold = 3;

	public ChSettings Settings;

	public Transform[] Legs;
	private float[] legArmStatus = new float[4];
	private Rigidbody[] legsConnectedBodies = new Rigidbody[4];
	
	protected bool LegOnGround => legArmStatus[0] == Catch || legArmStatus[1] == Catch;
	protected bool ArmCatched => legArmStatus[2] == Catch || legArmStatus[3] == Catch;
	protected bool ArmHolds => legArmStatus[2] == Hold || legArmStatus[3] == Hold;

	private Vector3 legUpDir = Vector3.up;

	public SphereCollider LegSphere;
	public SphereCollider ArmSphere;

	protected Rigidbody body;
	protected Placeable placeable;
	
	protected Vector2 desiredVelocity;
	protected bool desiredJump;
	protected float desiredZMove;
	protected bool desiredCatch;
	protected bool desiredHold;


	private static List<Vector2> armCandidates = new List<Vector2>();
	private static List<Placeable> placeables = new List<Placeable>();
	private static List<Collider> colliders1 = new List<Collider>();
	private static List<Collider> colliders2 = new List<Collider>();

	protected void AwakeB()
	{
		body = GetComponent<Rigidbody>();
		placeable = GetComponent<Placeable>();
		Settings.Initialize(ArmSphere, Legs);
	}

	protected void AdjustLegsArms()
	{
		Game.Map.Move(placeable);

		DoTimeouts();

		TryRemoveLeg(0);
		TryRemoveLeg(1);
		TryRemoveArm(2);
		TryRemoveArm(3);

		if (Vector3.Dot(body.velocity, legUpDir) <= 0 && SelectFreeLeg(out var index))
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
			if (legArmStatus[f] == Hold && legsConnectedBodies[f] != null)
			{
				for (int g = 0; g < legArmStatus.Length; g++)
				{
					if (legArmStatus[g] == Catch && legsConnectedBodies[f] == legsConnectedBodies[g])
						RemoveLeg(g);
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
			var lpos = Legs[index].position.XY();
			var center = LegSphere.transform.position.XY();
			var radius = desiredJump ? LegSphere.radius * 1.2f : LegSphere.radius;
			if ((lpos - center).sqrMagnitude > radius * radius)
			{
				RemoveLeg(index);
			}
			else if (legArmStatus[OtherIndex(index)] == Catch)
			{
				float otherX = Legs[OtherIndex(index)].position.x;
				if (lpos.x <= otherX && otherX < center.x && body.velocity.x >= 0)
					RemoveLeg(index);
				if (lpos.x >= otherX && otherX > center.x && body.velocity.x <= 0)
					RemoveLeg(index);
			}
		}
	}

	private void TryRemoveArm(int index)
	{
		if (legArmStatus[index] == Catch)
		{
			if (!desiredCatch)
			{
				RemoveLeg(index);
			}
			else
			{
				var lpos = Legs[index].position.XY();
				var center = ArmSphere.transform.position.XY();
				var radius = ArmSphere.radius;
				if ((lpos - center).sqrMagnitude > radius * radius)
				{
					RemoveLeg(index);
				}
				else if (legArmStatus[OtherIndex(index)] == Catch)
				{
					var dotPos1 = Vector2.Dot(desiredVelocity, lpos - center);
					if (dotPos1 < 0)
					{
						var dotPos2 = Vector2.Dot(desiredVelocity, Legs[OtherIndex(index)].position.XY() - center);
						if (dotPos1 <= dotPos2)
							RemoveLeg(index);
					}
				}
			}
		}
		else if (legArmStatus[index] == Hold)
		{
			if (!desiredHold)
			{
				if (legsConnectedBodies[index] != null)
					IgnoreCollision(legsConnectedBodies[index], false);
				RemoveLeg(index);
			}
			else
			{
				var lpos = Legs[index].position.XY();
				var center = ArmSphere.transform.position.XY();
				var radius = ArmSphere.radius;
				if ((lpos - center).sqrMagnitude > radius * radius)
				{
					if (legsConnectedBodies[index] != null)
						IgnoreCollision(legsConnectedBodies[index], false);
					RemoveLeg(index);
				}
			}
		}
	}


	private int OtherIndex(int index) => index ^ 1;

	private void RemoveLeg(int index)
	{
		Legs[index].gameObject.SetActive(false);
		Legs[index].SetParent(transform, true);
		legArmStatus[index] = Timeout;
		legsConnectedBodies[index] = null;
	}

	private void RemoveAllLegs()
	{
		if (legArmStatus[0] == Catch)
			RemoveLeg(0);
		if (legArmStatus[1] == Catch)
			RemoveLeg(1);
	}

	private void RemoveAllLegsArms()
	{
		for (int f = 0; f < Legs.Length; f++)
		{
			if (legArmStatus[f] == Catch)
				RemoveLeg(f);
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
			if (hitInfo.normal.y >= Settings.minGroundDotProduct)
			{
				PlaceLeg(index, ref hitInfo);
				return true;
			}
		}
		return false;
	}

	private void PlaceLeg(int index, ref RaycastHit hitInfo)
	{
		Legs[index].SetParent(FindPlaceableTransform(hitInfo.transform, out legsConnectedBodies[index]), true);
		Legs[index].position = new Vector3(hitInfo.point.x, hitInfo.point.y, Settings.legZ[index] + transform.position.z);
		Legs[index].rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
		Legs[index].gameObject.SetActive(true);
		legArmStatus[index] = Catch;
	}

	private void PlaceLeg3d(int index, ref RaycastHit hitInfo, float statusType)
	{
		Legs[index].SetParent(FindPlaceableTransform(hitInfo.transform, out legsConnectedBodies[index]), true);
		Legs[index].position = hitInfo.point;
		Legs[index].rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
		Legs[index].gameObject.SetActive(true);
		legArmStatus[index] = statusType;
	}

	private Transform FindPlaceableTransform(Transform t, out Rigidbody rb)
	{
		rb = null;
		var t2 = t;
		while (t2 != null && !t2.TryGetComponent<Rigidbody>(out rb))
			t2 = t2.parent;
		if (t2 != null)
			return t2;

		while (t.parent != null && !t.TryGetComponent<Placeable>(out _))
			t = t.parent;
		return t;
	}

	//private void PlaceLeg(int index, Vector3 point, Vector3 normal)
	//{
	//	Legs[index].position = new Vector3(point.x, point.y, legZ[index] + transform.position.z);
	//	Legs[index].rotation = Quaternion.FromToRotation(Vector3.up, normal);
	//	Legs[index].gameObject.SetActive(true);
	//	legArmStatus[index] = 2;
	//}

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

		var radius2 = new Vector2(ArmSphere.radius, ArmSphere.radius);
		map.Get(placeables, center - radius2, 2 * radius2, KsidEnum.Catch);

		foreach (var p in placeables)
		{
			var col = p.GetComponentInChildren<Collider>();
			if (col != null)
			{
				var center3d = ArmSphere.transform.position + new Vector3(0, 0, Settings.legZ[index]);
				var pos = col.ClosestPoint(center3d);
				if (!otherPlaced || Vector2.Dot(desiredVelocity, pos - center3d) >= 0)
				{
					if (Physics.Raycast(center3d, pos - center3d, out var hitInfo, ArmSphere.radius, Settings.armCatchLayerMask))
					{
						if ((hitInfo.point - pos).sqrMagnitude < 0.001)
						{
							PlaceLeg3d(index, ref hitInfo, Catch);
							placeables.Clear();
							return;
						}
					}
				}
			}
		}

		placeables.Clear();

		var c = map.WorldToCell(ArmSphere.transform.position.XY());
		var c1 = c - Settings.ArmCellRadius;
		var c2 = c + Settings.ArmCellRadius + Vector2Int.one;

		var blocking = transform.ToFullBlock();

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
		var map = Game.Map;
		var center = ArmSphere.transform.position.XY();
		var radius2 = new Vector2(ArmSphere.radius, ArmSphere.radius);
		map.Get(placeables, center - radius2, 2 * radius2, Settings.HoldType);

		foreach (var p in placeables)
		{
			var col = p.GetComponentInChildren<Collider>();
			if (col != null)
			{
				var center3d = ArmSphere.transform.position + new Vector3(0, 0, Settings.legZ[index]);
				var pos = col.ClosestPoint(center3d);
				if (Physics.Raycast(center3d, pos - center3d, out var hitInfo, ArmSphere.radius, Settings.armCatchLayerMask))
				{
					if ((hitInfo.point - pos).sqrMagnitude < 0.001)
					{
						PlaceLeg3d(index, ref hitInfo, Hold);
						if (legsConnectedBodies[index] != null)
							IgnoreCollision(legsConnectedBodies[index], true);
						break;
					}
				}
			}
		}

		placeables.Clear();
	}

	private void IgnoreCollision(Rigidbody other, bool ignore)
	{
		GetComponentsInChildren<Collider>(colliders1);
		other.GetComponentsInChildren<Collider>(colliders2);

		foreach (var c1 in colliders1)
			foreach (var c2 in colliders2)
				Physics.IgnoreCollision(c2, c1, ignore);

		colliders1.Clear();
		colliders2.Clear();
	}

	private bool RayCastArm(int index, Vector3 candidate, float radius)
	{
		if (Physics.Raycast(ArmSphere.transform.position, candidate - ArmSphere.transform.position, out var hitInfo, radius, Settings.armCatchLayerMask))
		{
			if ((hitInfo.point - candidate).sqrMagnitude < 0.001)
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


	void FixedUpdate()
	{
		Vector3 groundVelocity = GetGroundVelocity();
		if (ArmCatched)
		{
			var force = Vector2.ClampMagnitude(groundVelocity.XY() + desiredVelocity - body.velocity.XY(), Settings.maxAcceleration);
			body.AddForce(force, ForceMode.VelocityChange);
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
			body.AddForce(xAxis * force, ForceMode.VelocityChange);
		}

		body.AddForce(GetArmsCatchForce(), ForceMode.VelocityChange);

		bool jumpStarted = false;
		if (desiredJump)
		{
			if (LegOnGround)
			{
				var jumpForce = Mathf.Sqrt(-2f * Physics.gravity.y * Settings.jumpHeight) - body.velocity.y;
				body.AddForce(0, jumpForce, 0, ForceMode.VelocityChange);
				SendOppositeForceToLegs(new Vector3(0, jumpForce, 0));
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
			SendOppositeForceToLegs(legF);
			float legSF = GetLegSideForce();
			body.AddForce(legSF, 0, 0, ForceMode.VelocityChange);
		}

		if (!jumpStarted)
			body.AddForce(GetDrag());

		if (desiredZMove != 0)
		{
			var p = transform.position;
			p.z += desiredZMove;
			transform.position = p;
			desiredZMove = 0;
			RemoveAllLegsArms();
			ActivateSomeLegsArms();
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
		var body = legsConnectedBodies[index];
		if (body != null) 
		{
			var armPos = Legs[index].position.XY();
			var destPos = ArmSphere.transform.position.XY();
			if (destPos.x < armPos.x)
			{
				destPos += Settings.HoldPosition;
			}
			else
			{
				destPos += new Vector2(-Settings.HoldPosition.x, Settings.HoldPosition.y);
			}
			destPos += this.body.velocity.XY() * Time.fixedDeltaTime;

			var dist = (destPos - armPos) * Settings.HoldMoveSpeed;
			var force = Vector2.ClampMagnitude(dist - body.velocity.XY(), Settings.HoldMoveAcceleration);
			body.AddForce(force, ForceMode.VelocityChange);
		}
	}

	private Vector3 GetGroundVelocity()
	{
		int count = 0;
		Vector3 res = Vector3.zero;
		for (int f = 0; f < Legs.Length; f++)
		{
			if (legArmStatus[f] == Catch)
			{
				count++;
				if (legsConnectedBodies[f] != null)
					res += legsConnectedBodies[f].velocity;
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

	private Vector2 GetArmsCatchForce()
	{
		var dot = 1 - Mathf.Clamp(Vector2.Dot(body.velocity.XY(), desiredVelocity), 0, 1);
		Vector2 forceToReduce = dot * body.velocity.XY();
		Vector2 result = Vector2.zero;
		GetArmCatchForce(2, ref forceToReduce, ref result);
		GetArmCatchForce(3, ref forceToReduce, ref result);
		return result;
	}

	private void GetArmCatchForce(int index, ref Vector2 forceToReduce, ref Vector2 result)
	{
		if (legArmStatus[index] == Catch)
		{
			var armDir = (ArmSphere.transform.position.XY() - Legs[index].position.XY());
			var armDirNorm = armDir.normalized;
			var holdVel = Vector2.Dot(armDirNorm, forceToReduce) + 1;
			var inForce = armDir.sqrMagnitude / (ArmSphere.radius * ArmSphere.radius);
			inForce = Mathf.Max(0, inForce - 0.7f * 0.7f);
			inForce *= Settings.ArmInForceCoef * holdVel;
			var armForce = -inForce * armDirNorm;
			result += armForce;
			forceToReduce += armForce;
			SendOppositeForce(armForce, index);
		}
	}

	private void SendOppositeForceToLegs(Vector3 velocity)
	{
		if (legArmStatus[0] == Catch)
		{
			if (legArmStatus[1] == Catch)
			{
				SendOppositeForce(velocity * 0.5f, 0);
				SendOppositeForce(velocity * 0.5f, 1);
			}
			else
			{
				SendOppositeForce(velocity, 0);
			}
		}
		else if (legArmStatus[1] == Catch)
		{
			SendOppositeForce(velocity, 1);
		}
	}

	private void SendOppositeForce(Vector3 vector3, int index)
	{
		legsConnectedBodies[index]?.AddForce(-vector3, ForceMode.VelocityChange);
	}
}

