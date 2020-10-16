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
	[SerializeField, Range(0f, 100f)]
	public float maxSpeed = 5f;

	[SerializeField, Range(0f, 100f)]
	float maxAcceleration = 0.3f;

	[SerializeField, Range(0f, 10f)]
	float jumpHeight = 1.2f;

	public float LegForce = 1.5f;
	public float LegSideLimit = 3;
	public float DragCoef = 1f;
	public float MaxArmHoldVel = 1f;

	const float maxGroundAngle = 40f;
	private float minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);

	public Transform[] Legs;
	private float[] legArmStatus = new float[4];
	protected bool LegOnGround => legArmStatus[0] == 2 || legArmStatus[1] == 2;
	protected bool ArmCatched => legArmStatus[2] == 2 || legArmStatus[3] == 2;
	private float[] legZ;

	private Vector3 legUpDir = Vector3.up;

	public SphereCollider LegSphere;
	public SphereCollider ArmSphere;

	protected Rigidbody body;
	protected Vector2 desiredVelocity;
	protected bool desiredJump;

	protected float desiredZMove;

	protected bool desiredCatch;

	private Vector2Int armCellRadius;
	private int armCatchLayerMask;

	private static List<Vector2> armCandidates = new List<Vector2>();
	private static List<Placeable> placeables = new List<Placeable>();

	protected void AwakeB()
	{
		body = GetComponent<Rigidbody>();
		armCellRadius.x = Mathf.CeilToInt(ArmSphere.radius * Map.CellSize2dInv.x);
		armCellRadius.y = Mathf.CeilToInt(ArmSphere.radius * Map.CellSize2dInv.y);

		legZ = Legs.Select(l => l.position.z).ToArray();
		armCatchLayerMask = LayerMask.GetMask("Default", "Catches");
	}

	protected void AdjustLegsArms()
	{
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
	}


	private void DoTimeouts()
	{
		for (int f = 0; f < legArmStatus.Length; f++)
		{
			if (legArmStatus[f] <= 1)
				legArmStatus[f] -= Time.deltaTime * maxSpeed * 1.3f;
		}
	}

	private void TryRemoveLeg(int index)
	{
		if (legArmStatus[index] == 2)
		{
			var lpos = Legs[index].position.XY();
			var center = LegSphere.transform.position.XY();
			var radius = desiredJump ? LegSphere.radius * 1.2f : LegSphere.radius;
			if ((lpos - center).sqrMagnitude > radius * radius)
			{
				RemoveLeg(index);
			}
			else if (legArmStatus[OtherIndex(index)] == 2)
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
		if (legArmStatus[index] == 2)
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
				else if (legArmStatus[OtherIndex(index)] == 2)
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
	}


	private int OtherIndex(int index) => index ^ 1;

	private void RemoveLeg(int index)
	{
		Legs[index].gameObject.SetActive(false);
		legArmStatus[index] = 1;
	}

	private void RemoveAllLegs()
	{
		if (legArmStatus[0] == 2)
			RemoveLeg(0);
		if (legArmStatus[1] == 2)
			RemoveLeg(1);
	}

	private void RemoveAllLegsArms()
	{
		for (int f = 0; f < Legs.Length; f++)
		{
			if (legArmStatus[f] == 2)
				RemoveLeg(f);
		}
	}

	private void ActivateSomeLegsArms()
	{
		if (legArmStatus[0] > 0 && legArmStatus[1] > 0 && !LegOnGround)
		{
			if (legArmStatus[0] < legArmStatus[1])
			{
				legArmStatus[0] = 0;
			}
			else
			{
				legArmStatus[1] = 0;
			}
		}

		if (legArmStatus[1] > 0 && legArmStatus[2] > 0 && !ArmCatched)
		{
			if (legArmStatus[1] < legArmStatus[2])
			{
				legArmStatus[1] = 0;
			}
			else
			{
				legArmStatus[2] = 0;
			}
		}
	}

	private void TryPlaceLeg(int index)
	{
		if (legArmStatus[OtherIndex(index)] == 2)
		{
			float otherX = Legs[OtherIndex(index)].position.x;
			var centerX = LegSphere.transform.position.x;
			if (otherX > centerX && body.velocity.x > 0)
				return;
			if (otherX < centerX && body.velocity.x < 0)
				return;
		}

		Vector3 direction = Vector3.down * maxSpeed + body.velocity;
		if (RayCastLeg(index, direction, LegSphere.radius))
			return;

		float radius = desiredJump ? LegSphere.radius * 1.2f : LegSphere.radius * 0.7f;
		if (RayCastLeg(index, Vector3.down, radius))
			return;

		if (desiredJump && legArmStatus[OtherIndex(index)] != 2)
		{
			direction = new Vector3(Mathf.Sign(body.velocity.x) * -0.5f, -1f);
			if (RayCastLeg(index, direction, radius))
				return;
		}
	}

	private bool RayCastLeg(int index, Vector3 direction, float radius)
	{
		if (Physics.Raycast(LegSphere.transform.position, direction, out var hitInfo, radius))
		{
			if (hitInfo.normal.y >= minGroundDotProduct)
			{
				PlaceLeg(index, ref hitInfo);
				return true;
			}
		}
		return false;
	}

	private void PlaceLeg(int index, ref RaycastHit hitInfo)
	{
		Legs[index].position = new Vector3(hitInfo.point.x, hitInfo.point.y, legZ[index] + transform.position.z);
		Legs[index].rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
		Legs[index].gameObject.SetActive(true);
		legArmStatus[index] = 2;
	}

	private void PlaceLeg3d(int index, ref RaycastHit hitInfo)
	{
		Legs[index].position = hitInfo.point;
		Legs[index].rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
		Legs[index].gameObject.SetActive(true);
		legArmStatus[index] = 2;
	}

	private void PlaceLeg(int index, Vector3 point, Vector3 normal)
	{
		Legs[index].position = new Vector3(point.x, point.y, legZ[index] + transform.position.z);
		Legs[index].rotation = Quaternion.FromToRotation(Vector3.up, normal);
		Legs[index].gameObject.SetActive(true);
		legArmStatus[index] = 2;
	}

	private bool SelectFreeLeg(out int index) => SelectFreeLegArm(out index, 0, 1);
	private bool SelectFreeArm(out int index) => SelectFreeLegArm(out index, 2, 3);

	private bool SelectFreeLegArm(out int index, int i1, int i2)
	{
		if (legArmStatus[i1] <= 0 && legArmStatus[i1] <= legArmStatus[i2])
		{
			index = i1;
			return true;
		}
		else if (legArmStatus[i2] <= 0)
		{
			index = i2;
			return true;
		}
		index = -1;
		return false;
	}

	private void TryPlaceArm(int index)
	{
		var map = Game.Instance.Level.Map;
		bool otherPlaced = ArmCatched;
		var center = ArmSphere.transform.position.XY();

		var radius2 = new Vector2(ArmSphere.radius, ArmSphere.radius);
		map.Get(placeables, center - radius2, 2 * radius2, KsidEnum.Catch);

		foreach (var p in placeables)
		{
			if (p.TryGetComponent<Collider>(out var col))
			{
				var center3d = ArmSphere.transform.position + new Vector3(0, 0, legZ[index]);
				var pos = col.ClosestPoint(center3d);
				if (!otherPlaced || Vector2.Dot(desiredVelocity, pos - center3d) >= 0)
				{
					if (Physics.Raycast(center3d, pos - center3d, out var hitInfo, ArmSphere.radius, armCatchLayerMask))
					{
						if ((hitInfo.point - pos).sqrMagnitude < 0.001)
						{
							PlaceLeg3d(index, ref hitInfo);
							placeables.Clear();
							return;
						}
					}
				}
			}
		}

		placeables.Clear();

		var c = map.WorldToCell(ArmSphere.transform.position.XY());
		var c1 = c - armCellRadius;
		var c2 = c + armCellRadius + Vector2Int.one;

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

	private bool RayCastArm(int index, Vector3 candidate, float radius)
	{
		if (Physics.Raycast(ArmSphere.transform.position, candidate - ArmSphere.transform.position, out var hitInfo, radius, armCatchLayerMask))
		{
			if ((hitInfo.point - candidate).sqrMagnitude < 0.001)
			{
				PlaceLeg(index, ref hitInfo);
				return true;
			}
		}
		else if ((ArmSphere.transform.position - candidate).sqrMagnitude <= radius * radius)
		{
			PlaceLeg(index, candidate, (candidate - ArmSphere.transform.position).normalized);
			return true;
		}
		return false;
	}


	void FixedUpdate()
	{
		if (ArmCatched)
		{
			var force = Vector2.ClampMagnitude(desiredVelocity - body.velocity.XY(), maxAcceleration);
			body.AddForce(force, ForceMode.VelocityChange);
			legUpDir = Vector3.up;
		}
		else
		{
			Quaternion legRot = GetLegRotation();
			var xAxis = legRot * Vector3.right;
			legUpDir = legRot * Vector3.up;
			var xVel = Vector3.Dot(xAxis, body.velocity);
			var force = Mathf.Clamp(desiredVelocity.x - xVel, -maxAcceleration, maxAcceleration);
			body.AddForce(xAxis * force, ForceMode.VelocityChange);
		}

		body.AddForce(GetArmsHoldForce(), ForceMode.VelocityChange);

		bool jumpStarted = false;
		if (desiredJump)
		{
			if (LegOnGround)
			{
				body.AddForce(0, Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight) - body.velocity.y, 0, ForceMode.VelocityChange);
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
			float legF = Mathf.Max(GetLegForce(0), GetLegForce(1));
			body.AddForce(legUpDir * legF, ForceMode.VelocityChange);
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
	}

	private Quaternion GetLegRotation()
	{
		if (Physics.Raycast(LegSphere.transform.position, -legUpDir, out var hitInfo, LegSphere.radius * 1.3f))
		{
			if (hitInfo.normal.y >= minGroundDotProduct)
			{
				return Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
			}
		}

		return Quaternion.identity;
	}

	private Vector3 GetDrag()
	{
		return body.velocity * -body.velocity.magnitude * DragCoef;
	}

	private float GetLegForce(int index)
	{
		if (legArmStatus[index] == 2)
		{
			var legDir = LegSphere.transform.position.XY() - Legs[index].position.XY();
			float force = (LegSphere.radius - legDir.magnitude) / LegSphere.radius;
			force *= LegForce;
			force += Vector3.Dot(legUpDir, body.velocity) * -0.3f;
			return force;
		}
		return 0;
	}

	private float GetLegSideForce()
	{
		float force = 0;
		force += GetLegSideForce(0);
		force += GetLegSideForce(1);
		float limit = LegSideLimit - Math.Min(Math.Abs(desiredVelocity.x), LegSideLimit);
		return Mathf.Clamp(force, -limit, limit);
	}

	private float GetLegSideForce(int index)
	{
		if (legArmStatus[index] == 2)
		{
			var delta = (LegSphere.transform.position.x - Legs[index].position.x) / LegSphere.radius;
			return delta * delta * delta * LegSideLimit;
		}
		return 0;
	}

	private Vector2 GetArmsHoldForce()
	{
		var dot = 1 - Mathf.Clamp(Vector2.Dot(body.velocity.XY(), desiredVelocity), 0, 1);
		Vector2 forceToReduce = dot * body.velocity.XY();
		Vector2 result = Vector2.zero;
		GetArmHoldForce(2, ref forceToReduce, ref result);
		GetArmHoldForce(3, ref forceToReduce, ref result);
		return result;
	}

	private void GetArmHoldForce(int index, ref Vector2 forceToReduce, ref Vector2 result)
	{
		if (legArmStatus[index] == 2)
		{
			var armDir = (ArmSphere.transform.position.XY() - Legs[index].position.XY());
			var armDirNorm = armDir.normalized;
			var holdVel = Vector2.Dot(armDirNorm, forceToReduce);
			var inForce = armDir.magnitude / ArmSphere.radius;
			inForce *= inForce * 0.5f;
			holdVel = Mathf.Clamp(holdVel + inForce, -MaxArmHoldVel, MaxArmHoldVel);
			result += -holdVel * armDirNorm;
			forceToReduce += result;
		}
	}
}

