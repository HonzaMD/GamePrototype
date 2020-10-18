using Assets.Scripts;
using Assets.Scripts.Core;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityTemplateProjects;

[RequireComponent(typeof(PlaceableCellPart), typeof(Rigidbody))]
public class Character3 : ChLegsArms, IActiveObject
{
	[HideInInspector]
	public SimpleCameraController Camera { get; set; }

	private Vector3 oldPos;
	private float lastJumpTime;

	private float zMoveTimeout;

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

		if (zMoveTimeout > 0)
			zMoveTimeout -= Time.deltaTime * 3f;

		if (ArmCatched)
			desiredJump = false;

		float inX = Mathf.Clamp(Input.GetAxis("Horizontal"), -1, 1);
		float inY = Mathf.Clamp(Input.GetAxis("Vertical"), -1, 1);
		var speedMode =  (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? 0.5f : ArmCatched ? 0.6f : 1f;

		if (ArmCatched)
		{
			desiredVelocity = new Vector2(inX, inY) * Settings.maxSpeed * speedMode;
		}
		else
		{
			desiredVelocity.x = inX * Settings.maxSpeed * speedMode;
			desiredVelocity.y = 0;
		}


		if (zMoveTimeout <= 0 && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
		{
			desiredZMove = transform.position.z == 0 ? Map.CellSize.z : -Map.CellSize.z;
			zMoveTimeout = 1;
		}
	}
}