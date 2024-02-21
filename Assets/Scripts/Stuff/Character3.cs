﻿using Assets.Scripts;
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
using UnityTemplateProjects;

[RequireComponent(typeof(PlaceableSibling), typeof(Rigidbody))]
public class Character3 : ChLegsArms, IActiveObject, IInventoryAccessor, ILevelPlaceabe
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
			InventoryAccess(Game.Instance.PrefabsStore.Gravel);
		} 
		else if (Input.GetKeyDown(KeyCode.Alpha2) && !throwActive)
		{
			InventoryAccess(Game.Instance.PrefabsStore.StickyBomb);
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
				inventoryHoldAttempts = 0;
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

	private void InventoryAccess(Label prototype)
	{
		if (ArmHolds)
			RecatchHold();
		desiredHold = true;
		inventoryAccessor = this;
		inventoryPrototype = prototype;
		inventoryHoldAttempts = 3;
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
					var body = GetHoldObject();
					if (body != null)
					{
						ActivateByThrow(body);
						bodyToThrow = body.Rigidbody;
						holdTarget = Vector2.zero;
						desiredHold = false;
					}
				}
			}
		}
	}

	private void ActivateByThrow(Label body)
	{
		if (body.KsidGet.IsChildOf(Ksid.ActivatesByThrow) && body.TryGetComponent(out ICanActivate ao))
			ao.Activate();
	}

	private void PositionLongThrowMarker()
	{
		var body = GetHoldObject();
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
				var body = GetHoldObject();
				if (body != null)
                {
                    marker.transform.position = body.transform.position;
                    var mBody = marker.GetComponent<Rigidbody>();
					var throwMass = body.Rigidbody.mass;
                    mBody.mass = throwMass;
                    Vector2 force = ComputeThrowForce(throwMass);
                    mBody.velocity = (Vector3)force + this.body.velocity;
                }
            }
		} 
		else
		{
			throwTimer -= Time.deltaTime;
		}
	}

    private Vector2 ComputeThrowForce(float throwMass)
    {
		var koef = Mathf.Min(5.5f, 10f / Mathf.Sqrt(throwMass));
        return new Vector2(Mathf.Cos(throwAngle), Mathf.Sin(throwAngle)) * (1 + throwForce) * koef;
    }

    public override void GameFixedUpdate()
    {
		if (bodyToThrow == null)
			base.GameFixedUpdate();
		if (throwActive)
			ShowThrowMarker();
		if (bodyToThrow != null)
		{
			Vector2 force = ComputeThrowForce(bodyToThrow.mass);
			bodyToThrow.velocity = (Vector3)force + this.body.velocity;
            body.AddForce(-force, bodyToThrow.mass, VelocityFlags.None);
            bodyToThrow = null;
        }
    }

	Label IInventoryAccessor.InventoryGet()
	{
		if (inventoryObj != null)
			return inventoryObj;
		Vector3 pos = holdTarget != Vector2.zero
			? ArmSphere.transform.position + holdTarget.AddZ(0)
			: ArmSphere.transform.position + new Vector3(Settings.HoldPosition.x * lastXOrientation, Settings.HoldPosition.y, 0);
		var l = inventoryPrototype.Create(placeable.LevelGroup, pos);
		l.PlaceableC.PlaceToMap(map);
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

    void ILevelPlaceabe.Instantiate(Map map, Transform parent, Vector3 pos)
    {
		var placeable = GetComponent<Placeable>();
		if (!placeable.IsMapPlaced)
		{
			transform.parent = parent;
			placeable.LevelPlaceAfterInstanciate(map, pos);
		}
    }
    bool ILevelPlaceabe.SecondPhase => false;
}