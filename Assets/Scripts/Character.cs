using Assets.Scripts;
using Assets.Scripts.Core;
using Assets.Scripts.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityTemplateProjects;

public class Character : CharacterBase
{
	[SerializeField, Range(0f, 100f)]
	float maxSpeed = 10f;

	[SerializeField, Range(0f, 100f)]
	float maxAcceleration = 10f;

	[SerializeField, Range(0f, 100f)]
	float maxAirAcceleration = 1f;

	[SerializeField, Range(0f, 10f)]
	float jumpHeight = 2f;

	[SerializeField, Range(0, 90)]
	float maxGroundAngle = 40f;

	Vector3 velocity;
	float desiredVelocity;
	Rigidbody body;

	bool desiredJump;
	bool onGround;
	bool allCollidersAreBelow;
	bool collidingFromLeft;
	bool collidingFromRight;
	//Vector3 contactNormal;
	float minGroundDotProduct;
	Vector3 oldPos;
	private bool goLeft;

	Animator animator;
	private int speedId;
	private int isRunnningId;
	private int isJumping;
	private int crouching;
	Transform model;
	float animSpeed;
	float crouchingVal;
	float deltaY;
	private Transform feetBaseL;
	private Transform feetBaseR;

	private Transform colIdle;
	private Transform colRun;
	private Transform colCrouch;
	private Transform selectedCollider;
	private float colliderZoffset;
	private int defaultMask;

	private static readonly Collider[] colliderResults = new Collider[8];

	//public Character()
	//{
	//	Size = new Vector3(0.5f, 2f);
	//	Ksid = KsidEnum.Character;
	//	CellBlocking = CellBLocking.Cell1Part;
	//}

	void OnValidate()
	{
		minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
	}

	void Awake()
	{
		body = GetComponent<Rigidbody>();
		OnValidate();
		oldPos = transform.position;
		model = transform.Find("Model");
		animator = model.GetComponentInChildren<Animator>();
		speedId = Animator.StringToHash("speed");
		isRunnningId = Animator.StringToHash("isRunning");
		isJumping = Animator.StringToHash("isJumping");
		crouching = Animator.StringToHash("crouching");
		feetBaseL = transform.GetComponentsInChildren<Transform>().First(t => t.name == "FeetBaseL");
		feetBaseR = transform.GetComponentsInChildren<Transform>().First(t => t.name == "FeetBaseR");

		selectedCollider = colIdle = transform.GetComponentsInChildren<Transform>().First(t => t.name == "ColliderIdle");
		colRun = transform.GetComponentsInChildren<Transform>().First(t => t.name == "ColliderRun");
		colCrouch = transform.GetComponentsInChildren<Transform>().First(t => t.name == "ColliderCrouch");
		defaultMask = LayerMask.GetMask("Default");
	}

	public override void GameUpdate()
	{
		var crouch = Input.GetAxis("Vertical") < -0.2f && onGround;
		var oldCV = crouchingVal;
		crouchingVal = -Mathf.Clamp(Input.GetAxis("Vertical"), -1, 0);
		CanTransferToCrouchCollider();
		CanTransferFromCrouchCollider();
		if (oldCV != crouchingVal && !onGround)
		{
			deltaY += (crouchingVal - oldCV) * 0.35f;
		}
		
		float inX = Mathf.Clamp(Input.GetAxis("Horizontal"), -1, 1);
		var speedMode = crouch ? 0.3f : (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? 0.5f : 1f;
		desiredVelocity = inX * maxSpeed * speedMode;

		desiredJump |= Input.GetButtonDown("Jump");

		var delta = transform.position - oldPos;
		delta.z = 0;
		oldPos = transform.position;
		Camera.SetTransform(delta);
	
		var isRunning = Mathf.Abs(animSpeed) > 0.0001;

		animator.SetFloat(speedId, Mathf.Abs(animSpeed) * 0.6f);
		animator.SetBool(isRunnningId, isRunning);
		animator.SetBool(isJumping, !onGround);
		animator.SetFloat(crouching, crouchingVal);
		Debug.Log(crouchingVal);
	}

	void FixedUpdate()
	{
		ChangeOrientation();
		ApplyDeltaY();

		var lastX = velocity.x;
		velocity = body.velocity;
		if (onGround && allCollidersAreBelow)
			velocity.x = lastX;

		float maxSpeedChange = (onGround ? maxAcceleration : maxAirAcceleration) * Time.fixedDeltaTime;

		CanTransferToRunCollider();

		float velDeltaF = desiredVelocity - velocity.x;
		velDeltaF = Mathf.Clamp(velDeltaF, -maxSpeedChange, maxSpeedChange);
		var velDelta = new Vector3(velDeltaF, 0, 0);

		if (desiredJump)
		{
			desiredJump = false;
			Jump(ref velDelta);
		}

		velocity += velDelta;
		body.velocity = velocity;
		animSpeed = velocity.x;
		onGround = false;
		allCollidersAreBelow = true;
		collidingFromLeft = collidingFromRight = false;

		SelectCollider(crouchingVal > 0.5f ? colCrouch : IsSlowMoving ? colIdle : colRun);
	}

	private void CanTransferToRunCollider()
	{
		if (selectedCollider == colIdle && Mathf.Abs(desiredVelocity) / maxSpeed > 0.3f)
		{
			var col = colRun.GetComponent<BoxCollider>();
			var pos = colRun.TransformPoint(col.center);
			var size = (col.size - new Vector3(0, 0.1f, 0)) * 0.5f;
			int colCounts = Physics.OverlapBoxNonAlloc(pos, size, colliderResults, colRun.rotation, defaultMask, QueryTriggerInteraction.Ignore);
			if (colCounts > 1)
			{
				pos += new Vector3(Mathf.Sign(desiredVelocity) * 0.3f, 0);
				colCounts = Physics.OverlapBoxNonAlloc(pos, size, colliderResults, colRun.rotation, defaultMask, QueryTriggerInteraction.Ignore);
				if (colCounts > 1)
					desiredVelocity = Mathf.Clamp(desiredVelocity, -0.3f * maxSpeed, 0.3f * maxSpeed);
			}
		}
	}

	private void CanTransferFromCrouchCollider()
	{
		if (selectedCollider == colCrouch && crouchingVal < 0.7f)
		{
			var col = colIdle.GetComponent<BoxCollider>();
			var pos = colIdle.TransformPoint(col.center);
			var size = (col.size - new Vector3(0, 0.1f, 0)) * 0.5f;
			int colCounts = Physics.OverlapBoxNonAlloc(pos, size, colliderResults, colIdle.rotation, defaultMask, QueryTriggerInteraction.Ignore);
			if (colCounts > 1)
			{
				crouchingVal = 0.7f;
			}
		}
	}

	private void CanTransferToCrouchCollider()
	{
		if (selectedCollider != colCrouch && crouchingVal > 0.3f)
		{
			var col = colCrouch.GetComponent<BoxCollider>();
			var pos = colCrouch.TransformPoint(col.center);
			var size = (col.size - new Vector3(0, 0.1f, 0)) * 0.5f;
			int colCounts = Physics.OverlapBoxNonAlloc(pos, size, colliderResults, colCrouch.rotation, defaultMask, QueryTriggerInteraction.Ignore);
			if (colCounts > 1)
			{
				pos = colCrouch.TransformPoint(new Vector3(col.center.x, col.center.y, 0));
				colCounts = Physics.OverlapBoxNonAlloc(pos, size, colliderResults, colCrouch.rotation, defaultMask, QueryTriggerInteraction.Ignore);
				if (colCounts > 1)
				{
					pos = colCrouch.TransformPoint(new Vector3(col.center.x, col.center.y, -col.center.z));
					colCounts = Physics.OverlapBoxNonAlloc(pos, size, colliderResults, colCrouch.rotation, defaultMask, QueryTriggerInteraction.Ignore);
					if (colCounts > 1)
						crouchingVal = 0.3f;
				}
			}
		}
	}



	bool IsSlowMoving => Mathf.Abs(animSpeed) / maxSpeed < 0.31f;


	private void ChangeOrientation()
	{
		if (animSpeed < -0.01 && !goLeft && desiredVelocity < -0.05)
		{
			goLeft = true;
			model.rotation = Quaternion.Euler(0, -90f, 0);
			if (colliderZoffset != 0)
				transform.localPosition = transform.localPosition + new Vector3(colliderZoffset, 0, 0);
		}

		if (animSpeed > 0.01 && goLeft && desiredVelocity > 0.05)
		{
			goLeft = false;
			model.rotation = Quaternion.Euler(0, 90f, 0);
			if (colliderZoffset != 0)
				transform.localPosition = transform.localPosition + new Vector3(-colliderZoffset, 0, 0);
		}
	}

	private void ApplyDeltaY()
	{
		if (deltaY != 0)
		{
			transform.localPosition = transform.localPosition + new Vector3(0, deltaY, 0);
			deltaY = 0;
		}
	}

	private void OnCollisionEnter(Collision collision)
	{
		CollisionCheck(collision);
	}

	void OnCollisionStay(Collision collision)
	{
		CollisionCheck(collision);
	}

	public void CollisionCheck(Collision collision)
	{
		for (int i = 0; i < collision.contactCount; i++)
		{
			var contact = collision.GetContact(i);
			Vector3 normal = contact.normal;
			onGround |= normal.y >= minGroundDotProduct;
			var isBellow = contact.otherCollider.ClosestPoint(transform.position + new Vector3(0, 10, 0)).y - 0.2 < transform.position.y;
			collidingFromLeft |= normal.x >= minGroundDotProduct && !isBellow;
			collidingFromRight |= normal.x <= -minGroundDotProduct && !isBellow;
			if (!isBellow)
				allCollidersAreBelow = false;
		}
	}

	private void Jump(ref Vector3 velDelta)
	{
		if (onGround)
			velDelta.y += Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
			//velocity += contactNormal * Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
	}


	private void SelectCollider(Transform collider)
	{
		if (selectedCollider != collider)
		{
			selectedCollider.GetComponent<BoxCollider>().enabled = false;
			var coll = collider.GetComponent<BoxCollider>();
			coll.enabled = true;
			selectedCollider = collider;
			colliderZoffset = 2 * coll.center.z;
		}
	}
}
