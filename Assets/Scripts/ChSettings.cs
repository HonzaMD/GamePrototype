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
	public float MaxArmHoldVel = 1f;
	public float ArmInForceCoef = 0.5f;

	const float maxGroundAngle = 40f;
	public float minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);

	public int armCatchLayerMask;

	public bool monsterMoveOnGround = true;
}
