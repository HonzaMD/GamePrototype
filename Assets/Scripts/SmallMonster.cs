using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(PlaceableCellPart), typeof(Rigidbody))]
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
		else if (body.velocity.x * desiredDirection <= 0.001 || !WantMove())
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
			var map = Game.Map;
			var cell = map.WorldToCell(transform.position);
			var fullBlock = transform.ToFullBlock();

			if ((map.GetCellBlocking(cell + Vector2Int.down) & fullBlock) == fullBlock)
			{
				if (map.IsXNearNextCell(transform.position.x, desiredDirection))
				{
					if ((map.GetCellBlocking(cell + new Vector2Int(desiredDirection, -1)) & fullBlock) != fullBlock
					/*|| (map.GetCellBlocking(cell + new Vector2Int(desiredDirection, 0), placeable) & fullBlock) != 0*/)
						return false;
				}
			}
		}
			
		return true;
	}
}

