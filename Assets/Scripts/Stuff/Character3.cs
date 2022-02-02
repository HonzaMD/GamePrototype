using Assets.Scripts;
using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Core.Inventory;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityTemplateProjects;

[RequireComponent(typeof(PlaceableSibling), typeof(Rigidbody))]
public class Character3 : ChLegsArms, IActiveObject, IInventoryAccessor
{
	[HideInInspector]
	public SimpleCameraController Camera { get; set; }

	private Vector3 oldPos;
	private float lastJumpTime;

	private float zMoveTimeout;
	private bool dropHold;
	private float holdRotationAngle = 0;

	private bool throwActive;
	private float throwAngle = 1.1f;
	private float throwForce = 0.5f;
	private float throwTimer;
	private float throwRotationAngle = 0;
	private float throwForceDir = 0;
	private Rigidbody bodyToThrow;

	private Label inventoryPrototype;
	private Label inventoryObj;
	private int inventoryHoldAttempts;

	void Awake()
	{
		oldPos = transform.position;
		AwakeB();
	}

	public void GameUpdate()
	{
		var delta = transform.position - oldPos;
		delta.z = 0;
		oldPos = transform.position;
		Camera.SetTransform(delta);

		bool jumpButton = Input.GetButtonDown("Jump");
		bool catchButton = Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftAlt);

		if (Input.GetKeyDown(KeyCode.Alpha1) && !throwActive)
		{
			if (ArmHolds)
				RecatchHold();
			desiredHold = true;
			inventoryAccessor = this;
			inventoryPrototype = Game.Instance.PrefabsStore.Gravel;
			inventoryHoldAttempts = 3;
		}

		if (Input.GetKeyDown(KeyCode.V))
		{
			if (ArmHolds)
			{
				dropHold = true;
			}
			else
			{
				desiredHold = true;
				dropHold = false;
			}

			if (throwActive)
			{
				SetThrowActive(false, false);
				dropHold = false;
			}
		}
		if (Input.GetKeyUp(KeyCode.V))
		{
			holdRotationAngle = 0;
			if (!throwActive)
			{
				if (dropHold)
				{
					holdTarget = Vector2.zero;
					dropHold = false;
					desiredHold = false;
				}
				else
				{
					RecatchHold();
				}
			}
		}

		desiredCatch = catchButton;

		if (ArmCatched && desiredCatch)
		{
			desiredJump = false;
		}
		else
		{
			if (jumpButton)
			{
				desiredJump = true;
				lastJumpTime = Time.time;
			}
			else if (lastJumpTime + 0.3f < Time.time)
			{
				desiredJump = false;
			}
		}

		AdjustLegsArms();

		SetThrowActive((Input.GetKeyDown(KeyCode.B) ^ throwActive) && ArmHolds, Input.GetKeyDown(KeyCode.B));
		bool holdButton = Input.GetKey(KeyCode.V) && !throwActive;


		if (!holdButton && desiredHold && !ArmHolds && inventoryHoldAttempts == 0)
		{
			holdTarget = Vector2.zero;
			desiredHold = false;
		}

		if (inventoryHoldAttempts > 0)
		{
			inventoryHoldAttempts--;
			if (ArmHolds)
			{
				Debug.Log("Attempts Left: " + inventoryHoldAttempts);
				inventoryHoldAttempts = 0;
			}
		}

		desiredCrouch = holdButton && !ArmHolds;

		if (zMoveTimeout > 0)
			zMoveTimeout -= Time.deltaTime * 3f;

		if (ArmCatched)
			desiredJump = false;

		float inX = Mathf.Clamp(Input.GetAxis("Horizontal"), -1, 1);
		float inY = Mathf.Clamp(Input.GetAxis("Vertical"), -1, 1);
		var speedMode =  (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? 0.5f : (ArmCatched || desiredCrouch)? 0.6f : 1f;

		if (throwActive)
		{
			desiredVelocity = Vector2.zero;
		} 
		else if (ArmCatched)
		{
			desiredVelocity = new Vector2(inX, inY) * Settings.maxSpeed * speedMode;
		}
		else
		{
			desiredVelocity.x = inX * Settings.maxSpeed * speedMode;
			desiredVelocity.y = 0;
		}

		if (holdButton && ArmHolds && Mathf.Abs(inY) > 0.2f)
		{
			if (holdRotationAngle == 0)
			{
				holdRotationAngle = holdTarget.x > 0 ? 340 : -340;
			}
			var rot = Quaternion.AngleAxis(inY * holdRotationAngle * Time.deltaTime, Vector3.forward);
			holdTarget = rot * holdTarget;
			dropHold = false;
		}

		if (throwActive)
		{
			if (Mathf.Abs(inY) > 0.2f)
			{
				if (throwRotationAngle == 0)
				{
					throwRotationAngle = Mathf.Cos(throwAngle) > 0 ? 2f : -2f;
				}
				throwAngle += inY * throwRotationAngle * Time.deltaTime;
			}
			else
			{
				throwRotationAngle = 0;
			}
			if (Mathf.Abs(inX) > 0.2f)
			{
				if (throwForceDir == 0)
				{
					throwForceDir = Mathf.Cos(throwAngle) > 0 ? 1.5f : -1.5f;
				}
				throwForce = Mathf.Clamp01(throwForce + inX * Time.deltaTime * throwForceDir);
			}
			else
			{
				throwForceDir = 0;
			}
			PositionLongThrowMarker();
		}

		if (zMoveTimeout <= 0 && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
		{
			desiredZMove = transform.position.z < 0.25f ? Map.CellSize.z : -Map.CellSize.z;
			zMoveTimeout = 1;
		}
	}

	private void SetThrowActive(bool activate, bool throwIt)
	{
		if (throwActive != activate)
		{
			throwActive = activate;
			if (throwActive)
			{
				throwTimer = 0;
				throwRotationAngle = 0;
				throwForceDir = 0;
				Game.Instance.LongThrowMarker.gameObject.SetActive(true);
				PositionLongThrowMarker();
			}
			else
			{
				Game.Instance.ClearAllHoldMarkers();
				Game.Instance.LongThrowMarker.gameObject.SetActive(false);
				if (throwIt)
				{
					var body = GetHoldBody();
					if (body != null)
					{
						bodyToThrow = body.Rigidbody;
						holdTarget = Vector2.zero;
						desiredHold = false;
					}
				}
			}
		}
	}

	private void PositionLongThrowMarker()
	{
		var body = GetHoldBody();
		if (body != null)
		{
			Game.Instance.LongThrowMarker.position = body.transform.position;
			Game.Instance.LongThrowMarker.rotation = Quaternion.AngleAxis(Mathf.Rad2Deg * throwAngle, Vector3.forward);
			var child = Game.Instance.LongThrowMarker.GetChild(0);
			child.localScale = new Vector3((1 + throwForce) * 0.4f, child.localScale.y, child.localScale.z);
			child.localPosition = new Vector3((1 + throwForce) * 0.2f + 0.4f, child.localPosition.y, child.localPosition.z);
		}
	}

	private void ShowThrowMarker()
	{
		if (throwTimer <= 0)
		{
			var marker = Game.Instance.GetHoldMarker(1.8f);
			if (marker != null)
			{
				throwTimer = 0.3f;
				var body = GetHoldBody();
				if (body != null)
				{
					marker.transform.position = body.transform.position;
					var mBody = marker.GetComponent<Rigidbody>();
					mBody.mass = body.Rigidbody.mass;
					Vector2 force = new Vector2(Mathf.Cos(throwAngle), Mathf.Sin(throwAngle)) * (1 + throwForce) * 4.8f;
					mBody.velocity = (Vector3)force + this.body.velocity;
				}
			}
		} 
		else
		{
			throwTimer -= Time.deltaTime;
		}
	}


	new void FixedUpdate()
	{
		if (bodyToThrow == null)
			base.FixedUpdate();
		if (throwActive)
			ShowThrowMarker();
		if (bodyToThrow != null)
		{
			Vector2 force = new Vector2(Mathf.Cos(throwAngle), Mathf.Sin(throwAngle)) * (1 + throwForce) * 4.8f;
			bodyToThrow.velocity = (Vector3)force + this.body.velocity;
			bodyToThrow = null;
		}
	}

	Label IInventoryAccessor.InventoryGet()
	{
		if (inventoryObj != null)
			return inventoryObj;
		Vector3 pos = holdTarget != Vector2.zero 
			? ArmSphere.transform.position + holdTarget.AddZ(0)
			: (body.velocity.x > 0 ? ArmSphere.transform.position + Settings.HoldPosition.AddZ(0) 
			: ArmSphere.transform.position + new Vector3(-Settings.HoldPosition.x, Settings.HoldPosition.y, 0));
		var l = inventoryPrototype.Create(Game.Instance.Level.transform, pos);
		l.PlaceableC.PlaceToMap(Game.Map);
		inventoryObj = l;
		return l;
	}

	void IInventoryAccessor.InventoryReturn(Label label)
	{
		if (inventoryHoldAttempts == 0)
		{
			inventoryAccessor = null;
			inventoryObj = null;
		}
	}
}