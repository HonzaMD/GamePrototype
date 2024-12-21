using Assets.Scripts.Bases;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(PlaceableSibling), typeof(Rigidbody))]
class SmallMonster : ChLegsArms, IActiveObject
{
	private float turnTimeout = 0.5f;
	public int desiredDirection = -1;

	void Awake()
	{
		AwakeB();
	}

	public void GameUpdate()
	{
		if (turnTimeout > 0.5)
		{
			turnTimeout -= Time.deltaTime * 4;
		}
		else if (turnTimeout > 0)
		{
			turnTimeout -= Time.deltaTime * 4;
			desiredVelocity.x = Settings.maxSpeed * desiredDirection;
		}
		else if (body.linearVelocity.x * desiredDirection <= 0.001 || !WantMove())
		{
			desiredVelocity.x = 0;
			turnTimeout = 1;
			desiredDirection *= -1;
		}
		
		AdjustLegsArms();
	}

	private bool WantMove()
	{
		if (Settings.monsterMoveOnGround)
		{
			var cell = map.WorldToCell(transform.position);
			var surface = CellUtils.Combine(SubCellFlags.HasFloor, transform);

			if ((map.GetCellBlocking(cell + Vector2Int.down) & surface) != 0)
			{
				if (map.IsXNearNextCell(transform.position.x, desiredDirection))
				{
					if ((map.GetCellBlocking(cell + new Vector2Int(desiredDirection, -1)) & surface) == 0
					/*|| (map.GetCellBlocking(cell + new Vector2Int(desiredDirection, 0), placeable) & fullBlock) != 0*/)
						return false;
				}
			}
		}
			
		return true;
	}
}

