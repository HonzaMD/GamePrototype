using Assets.Scripts.Core;
using Assets.Scripts.Utils;
using NUnit.Framework;
using System.Collections.Generic;

// Ksid jmena tu slouzi jen jako symboly s vlastni hierarchii.
public class KsidTableResolverTests
{
    // radky: Food <- Fruit, Food2 <- Fruit (Fruit ma dva rodice), Food <- Stone
    private const Ksid Food = Ksid.InventoryItem;
    private const Ksid Food2 = Ksid.Explosive;
    private const Ksid Fruit = Ksid.Fruit;
    private const Ksid Stone = Ksid.Stone;
    // sloupce: Monster <- SmallMonster, Character samostatne
    private const Ksid Monster = Ksid.Monster;
    private const Ksid SmallMonster = Ksid.SmallMonster;
    private const Ksid Character = Ksid.Character;

    private Ksids ksids;
    private readonly List<int> winners = new();

    [SetUp]
    public void SetUp()
    {
        ksids = new Ksids(new[]
        {
            (Fruit, Food),
            (Fruit, Food2),
            (Stone, Food),
            (SmallMonster, Monster),
        });
    }

    private int Resolve(List<(Ksid, Ksid)> filled, Ksid row, Ksid col)
        => KsidTableResolver.Resolve(ksids, filled, row, col, winners);

    [Test]
    public void Empty_None()
    {
        Assert.AreEqual(0, Resolve(new List<(Ksid, Ksid)>(), Fruit, Monster));
    }

    [Test]
    public void Explicit_Wins()
    {
        var filled = new List<(Ksid, Ksid)> { (Food, Monster), (Fruit, SmallMonster), (Fruit, Monster) };
        Assert.AreEqual(1, Resolve(filled, Fruit, SmallMonster));
        Assert.AreEqual(1, winners[0]);
    }

    [Test]
    public void InheritsFromParentRow()
    {
        var filled = new List<(Ksid, Ksid)> { (Food, Monster) };
        Assert.AreEqual(1, Resolve(filled, Stone, Monster));
        Assert.AreEqual(0, winners[0]);
    }

    [Test]
    public void InheritsFromParentColumn()
    {
        var filled = new List<(Ksid, Ksid)> { (Fruit, Monster) };
        Assert.AreEqual(1, Resolve(filled, Fruit, SmallMonster));
        Assert.AreEqual(0, winners[0]);
    }

    [Test]
    public void SpecificOnBothAxesBeatsGeneral()
    {
        var filled = new List<(Ksid, Ksid)> { (Food, Monster), (Fruit, Monster), (Food, SmallMonster), (Fruit, SmallMonster) };
        Assert.AreEqual(1, Resolve(filled, Fruit, SmallMonster));
        Assert.AreEqual(3, winners[0]);
    }

    [Test]
    public void SpecificRowWinsWhenColumnSame()
    {
        var filled = new List<(Ksid, Ksid)> { (Food, Monster), (Fruit, Monster) };
        Assert.AreEqual(1, Resolve(filled, Fruit, SmallMonster));
        Assert.AreEqual(1, winners[0]);
    }

    [Test]
    public void Crossing_Conflict()
    {
        var filled = new List<(Ksid, Ksid)> { (Fruit, Monster), (Food, SmallMonster) };
        Assert.AreEqual(2, Resolve(filled, Fruit, SmallMonster));
    }

    [Test]
    public void CrossingResolvedByExplicitCell()
    {
        var filled = new List<(Ksid, Ksid)> { (Fruit, Monster), (Food, SmallMonster), (Fruit, SmallMonster) };
        Assert.AreEqual(1, Resolve(filled, Fruit, SmallMonster));
        Assert.AreEqual(2, winners[0]);
    }

    [Test]
    public void TwoUnrelatedParents_Conflict()
    {
        var filled = new List<(Ksid, Ksid)> { (Food, Monster), (Food2, Monster) };
        Assert.AreEqual(2, Resolve(filled, Fruit, Monster));
    }

    [Test]
    public void TwoUnrelatedParents_OnlyOneMatchesOtherChild()
    {
        var filled = new List<(Ksid, Ksid)> { (Food, Monster), (Food2, Monster) };
        Assert.AreEqual(1, Resolve(filled, Stone, Monster));
        Assert.AreEqual(0, winners[0]);
    }

    [Test]
    public void UnrelatedCellsIgnored()
    {
        var filled = new List<(Ksid, Ksid)> { (Food, Character), (Stone, Monster) };
        Assert.AreEqual(0, Resolve(filled, Fruit, Monster));
    }

    [Test]
    public void ParentDoesNotSeeChildCell()
    {
        var filled = new List<(Ksid, Ksid)> { (Fruit, SmallMonster) };
        Assert.AreEqual(0, Resolve(filled, Food, Monster));
    }
}
