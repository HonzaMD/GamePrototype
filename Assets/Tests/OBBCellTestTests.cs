using Assets.Scripts.Map;
using NUnit.Framework;
using UnityEngine;

public class OBBCellTestTests
{
    [Test]
    public void AxisAligned_IntersectsAllCellsInBounds()
    {
        // 1x1 box at origin, axis-aligned, cell size 0.5
        var c0 = new Vector2(0, 0);
        var c1 = new Vector2(1, 0);
        var c2 = new Vector2(1, 1);
        var c3 = new Vector2(0, 1);
        var cellSize = new Vector2(0.5f, 0.5f);

        var test = new OBBCellTest(c0, c1, c2, c3, cellSize);

        // All 4 cells inside should intersect
        Assert.IsTrue(test.Intersects(0f, 0f));
        Assert.IsTrue(test.Intersects(0.5f, 0f));
        Assert.IsTrue(test.Intersects(0f, 0.5f));
        Assert.IsTrue(test.Intersects(0.5f, 0.5f));

        // Cells outside should not
        Assert.IsFalse(test.Intersects(1f, 0f));
        Assert.IsFalse(test.Intersects(-0.5f, 0f));
        Assert.IsFalse(test.Intersects(0f, 1f));
        Assert.IsFalse(test.Intersects(0f, -0.5f));
    }

    [Test]
    public void Rotated45_SkipsCornersOfAABB()
    {
        // 1x1 box centered at (2,2), rotated 45 degrees
        float h = Mathf.Sqrt(2f) / 2f; // ~0.707
        var center = new Vector2(2f, 2f);
        var c0 = center + new Vector2(0, -h);    // bottom
        var c1 = center + new Vector2(h, 0);     // right
        var c2 = center + new Vector2(0, h);      // top
        var c3 = center + new Vector2(-h, 0);    // left

        var cellSize = new Vector2(0.5f, 0.5f);
        var test = new OBBCellTest(c0, c1, c2, c3, cellSize);

        // Center cell should intersect
        Assert.IsTrue(test.Intersects(1.75f, 1.75f));

        // Corner cells of AABB should NOT intersect (the diamond misses them)
        // AABB goes from ~1.293 to ~2.707. Cell grid (0.5) means Floor(1.293/0.5)*0.5 = 1.0
        // Bottom-left corner cell (1.0, 1.0)→(1.5, 1.5) — fully outside diamond
        Assert.IsFalse(test.Intersects(1.0f, 1.0f));
        // Top-right corner cell (2.5, 2.5)→(3.0, 3.0) — fully outside diamond
        Assert.IsFalse(test.Intersects(2.5f, 2.5f));
        // But the cell touching the diamond's bottom vertex should intersect
        Assert.IsTrue(test.Intersects(1.75f, 1.0f));
    }

    [Test]
    public void ThinRotatedRect_HitsOnlyDiagonalCells()
    {
        // Thin rectangle (0.3 wide, 2 long) at 45 degrees
        float cos45 = Mathf.Cos(Mathf.PI / 4f);
        float sin45 = Mathf.Sin(Mathf.PI / 4f);
        float halfW = 0.15f;
        float halfL = 1f;

        var center = new Vector2(3f, 3f);
        var dirL = new Vector2(cos45, sin45);
        var dirW = new Vector2(-sin45, cos45);

        var c0 = center - dirL * halfL - dirW * halfW;
        var c1 = center + dirL * halfL - dirW * halfW;
        var c2 = center + dirL * halfL + dirW * halfW;
        var c3 = center - dirL * halfL + dirW * halfW;

        var cellSize = new Vector2(0.5f, 0.5f);
        var test = new OBBCellTest(c0, c1, c2, c3, cellSize);

        // Center cell
        Assert.IsTrue(test.Intersects(2.75f, 2.75f));

        // Cell far off to the side should not intersect
        Assert.IsFalse(test.Intersects(1.5f, 3.5f));
        Assert.IsFalse(test.Intersects(4f, 2f));
    }

    [Test]
    public void WithCellSize_ProducesSameResultsForSameSize()
    {
        var c0 = new Vector2(0, 0);
        var c1 = new Vector2(1, 0);
        var c2 = new Vector2(1, 1);
        var c3 = new Vector2(0, 1);
        var cellSize = new Vector2(0.5f, 0.5f);

        var test1 = new OBBCellTest(c0, c1, c2, c3, cellSize);
        var test2 = test1.WithCellSize(cellSize);

        Assert.AreEqual(test1.Intersects(0f, 0f), test2.Intersects(0f, 0f));
        Assert.AreEqual(test1.Intersects(1f, 0f), test2.Intersects(1f, 0f));
    }

    [Test]
    public void SameCorners_DetectsIdenticalAndDifferent()
    {
        var c0 = new Vector2(0, 0);
        var c1 = new Vector2(1, 0);
        var c2 = new Vector2(1, 1);
        var c3 = new Vector2(0, 1);
        var cellSize = new Vector2(0.5f, 0.5f);

        var test1 = new OBBCellTest(c0, c1, c2, c3, cellSize);
        var test2 = new OBBCellTest(c0, c1, c2, c3, cellSize);
        var test3 = new OBBCellTest(c0 + Vector2.one * 0.1f, c1, c2, c3, cellSize);

        Assert.IsTrue(test1.SameCorners(test2));
        Assert.IsFalse(test1.SameCorners(test3));
    }
}
