using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(Placeable), typeof(Rigidbody))]
class SmallMonster : ChLegsArms, IActiveObject
{
	void Awake()
	{
		AwakeB();
	}

	public void GameUpdate()
	{
		desiredVelocity.x = -Settings.maxSpeed;
		AdjustLegsArms();
	}
}

