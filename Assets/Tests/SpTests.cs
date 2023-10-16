using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Core.StaticPhysics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class SpTests
{
    // A Test behaves as an ordinary method
    [Test]
    public void SpTestsSimplePasses()
    {
        var dm = new SpDataManager();
        var worker = new GraphWorker(dm);

        var i1 = dm.ReserveNodeIndex();
        var i2 = dm.ReserveNodeIndex();
        var i3 = dm.ReserveNodeIndex();

        var commands = new InputCommand []
        {
            new InputCommand() {Command = SpCommand.AddNode, indexA = i1, pointA = Vector2.zero, isAFixed = true},
            new InputCommand() {Command = SpCommand.AddNodeAndJoint, indexA = i2, pointA = Vector2.down, indexB = i1, forceA = Vector2.down},
            new InputCommand() {Command = SpCommand.AddNodeAndJoint, indexA = i3, pointA = Vector2.down + Vector2.right, indexB = i2, forceA = Vector2.down}
        };

        worker.ApplyChanges(commands);

        Assert.IsTrue(true);
    }

    // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
    // `yield return null;` to skip a frame.
    [UnityTest]
    public IEnumerator SpTestsWithEnumeratorPasses()
    {
        // Use the Assert class to test conditions.
        // Use yield to skip a frame.
        yield return null;
    }
}
