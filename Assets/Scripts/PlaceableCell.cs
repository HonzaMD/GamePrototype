using Assets.Scripts.Utils;
using UnityEngine;

class PlaceableCell : Placeable
{
    public PlaceableCell()
    {
        CellBlocking = CellBlocking.Cell1;
    }

    public override void RefreshCoordinates()
    {
        CellBlocking = transform.ToFullBlock();
        base.RefreshCoordinates();
    }
}

