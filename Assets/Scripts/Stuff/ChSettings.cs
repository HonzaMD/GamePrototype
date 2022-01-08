using Assets.Scripts.Core;
using Assets.Scripts.Map;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu]
public class ChSettings : ScriptableObject
{
	[SerializeField, Range(0f, 100f)]
	public float maxSpeed = 5f;

	[SerializeField, Range(0f, 100f)]
	public float maxAcceleration = 0.3f;

	[SerializeField, Range(0f, 10f)]
	public float jumpHeight = 1.2f;

	public float LegTimeout = 5 * 1.3f;

	public float LegForce = 1.5f;
	public float LegForceDampening = 0.3f;
	public float LegSideLimit = 3;
	public float DragCoef = 1f;
	public float ArmInForceCoef = 2f;

	public float maxGroundAngle = 40f;
	public float minGroundDotProduct { get; private set; }

	public int armCatchLayerMask { get; private set; }
	public int legStandLayerMask { get; private set; }

	public bool monsterMoveOnGround = true;

	[NonSerialized]
	private bool initialized;
	[NonSerialized]
	private Vector2Int armCellRadius;
	public float[] legZ { get; private set; }

	public Vector2 HoldPosition;
	public Ksid HoldType;
	public float HoldMoveSpeed = 1f;
	public float HoldMoveAcceleration = 0.2f;

	public void Initialize(SphereCollider ArmSphere, Transform[] Legs)
	{
		if (!initialized)
		{
			initialized = true;
			legZ = Legs.Select(l => l.position.z).ToArray();
			armCatchLayerMask = LayerMask.GetMask("Default", "Catches", "SmallObjs", "MovingObjs");
			legStandLayerMask = LayerMask.GetMask("Default", "Catches", "MovingObjs");
			armCellRadius.x = Mathf.CeilToInt(ArmSphere.radius * Map.CellSize2dInv.x);
			armCellRadius.y = Mathf.CeilToInt(ArmSphere.radius * Map.CellSize2dInv.y);
			minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
		}
	}

	public Vector2Int ArmCellRadius => armCellRadius;
}
