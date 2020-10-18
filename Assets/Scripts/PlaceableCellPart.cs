using Assets.Scripts.Utils;
using UnityEngine;

class PlaceableCellPart : Placeable
{
    public PlaceableCellPart()
    {
        CellBlocking = CellBlocking.Cell1Part;
    }

    public override void RefreshCoordinates()
    {
        CellBlocking = transform.ToPartBlock();
        base.RefreshCoordinates();
    }
}
