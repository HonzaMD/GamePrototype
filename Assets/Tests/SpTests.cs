using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Core.StaticPhysics;
using Assets.Scripts.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class SpTests
{
    // A Test behaves as an ordinary method
    [Test]
    public void SpTests1SipleBreak()
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

        var forces = new ForceCommand[]
            { };

        var output = new SpanList<OutputCommand>();
        var input2 = new SpanList<InputCommand>();

        worker.ApplyChanges(commands, forces, output);
        worker.GetBrokenEdgesBigOnly(input2, output);
        Assert.AreEqual(1, input2.Count);
        worker.ApplyChanges(input2.AsSpan(), forces, output);
        input2.Clear();
        worker.GetBrokenEdges(input2, output);
        Assert.AreEqual(0, input2.Count);

        Assert.IsTrue(output.AsSpan().ToArray().Any(c => c.Command == SpCommand.RemoveJoint));
        Assert.IsTrue(output.AsSpan().ToArray().Any(c => c.Command == SpCommand.FallNode));
    }

    //// A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
    //// `yield return null;` to skip a frame.
    //[UnityTest]
    //public IEnumerator SpTestsWithEnumeratorPasses()
    //{
    //    // Use the Assert class to test conditions.
    //    // Use yield to skip a frame.
    //    yield return null;
    //}

    [Test]
    public void SpTests2MomentTransfer()
    {
        var dm = new SpDataManager();
        var worker = new GraphWorker(dm);

        var input = new Node(dm, force: Vector2.down)
            .Connect(Vector2.right, 0)
            .Connect(Vector2.right, 0)
            .Connect(Vector2.up, 0, isFixed: true)
            .Build();

        var output = RunSP(worker, input);

        Assert.IsTrue(output.Count == 0);
        Assert.AreEqual(2, dm.GetJoint(2).moment);
        Assert.AreEqual(-1, dm.GetJoint(2).compress);
    }


    [Test]
    public void SpTests3Balance()
    {
        var dm = new SpDataManager();
        var worker = new GraphWorker(dm);

        var n1 = new Node(dm, isFixed: true)
            .Connect(Vector2.down, 0);
        n1.Connect(Vector2.right, 0, force: Vector2.down * 2);
        n1.Connect(Vector2.left, 0)
            .Connect(Vector2.left, 0, force: Vector2.down);
        var input = n1.Build();

        var output = RunSP(worker, input);

        Assert.AreEqual(0, dm.GetJoint(0).moment);
        Assert.AreEqual(-3, dm.GetJoint(0).compress);
        Assert.IsTrue(output.Count == 0);
    }

    private static SpanList<OutputCommand> RunSP(GraphWorker worker, SpanList<InputCommand> input)
    {
        var forces = new ForceCommand[] { };
        var output = new SpanList<OutputCommand>();
        var input2 = new SpanList<InputCommand>();
        worker.ApplyChanges(input.AsSpan(), forces, output);
        worker.GetBrokenEdgesBigOnly(input2, output);
        if (input2.Count > 0)
        {
            worker.ApplyChanges(input2.AsSpan(), forces, output);
            input2.Clear();
            worker.GetBrokenEdges(input2, output);
            if (input2.Count > 0)
            {
                worker.ApplyChanges(input2.AsSpan(), forces, output);
            }
        }
        return output;
    }

    [Test]
    public void SpTests4FreeFall()
    {
        var dm = new SpDataManager();
        var worker = new GraphWorker(dm);

        var input = new Node(dm)
            .Connect(Vector2.right, 0)
            .New(Vector2.right * 5)
            .Build();

        var output = RunSP(worker, input);

        Assert.AreEqual(4, output.Count);
        Assert.IsTrue(output.AsSpan().ToArray().Count(c => c.Command == SpCommand.FallNode) == 3);
        Assert.IsTrue(output.AsSpan().ToArray().Count(c => c.Command == SpCommand.FallEdge) == 1);
    }


    [Test]
    public void SpTests5Table()
    {
        var dm = new SpDataManager();
        var worker = new GraphWorker(dm);

        var input = new Node(dm, isFixed: true)
            .Connect(Vector2.up, 0)
            .Connect(Vector2.up, 0, force: Vector2.down)
            .Connect(Vector2.right, 0, force: Vector2.down)
            .Connect(Vector2.right, 0, force: Vector2.down)
            .Connect(Vector2.right, 0, force: Vector2.down)
            .Connect(Vector2.down, 0)
            .Connect(Vector2.down, 0, isFixed: true)
            .Build();

        var output = RunSP(worker, input);

        Assert.AreEqual(0, output.Count);
    }

    [Test]
    public void SpTests6Modify()
    {
        var dm = new SpDataManager();
        var worker = new GraphWorker(dm);

        var n1 = new Node(dm, force: Vector2.down);
        var input = n1
            .Connect(Vector2.right, 0, force: Vector2.down)
            .Connect(Vector2.right, 0, force: Vector2.down, isFixed: true)
            .Build();

        var output = RunSP(worker, input);
        Assert.AreEqual(0, output.Count);

        input = n1.Connect(Vector2.up, 0, isFixed: true).Build();

        output = RunSP(worker, input);
        Assert.AreEqual(0, output.Count);

        input = new SpanList<InputCommand>();
        n1.BreakNode(input);

        output = RunSP(worker, input);
        Assert.AreEqual(1, output.Count);
        Assert.AreEqual(SpCommand.FreeNode, output.AsSpan()[0].Command);
    }

    [Test]
    public void SpTests6ModifyShorten()
    {
        var dm = new SpDataManager();
        var worker = new GraphWorker(dm);

        var n1 = new Node(dm, isFixed: true);
        var n2 = n1.Connect(Vector2.left, 0).Connect(Vector2.up, 0);
        var input = n2.Connect(Vector2.up, 0, force: Vector2.down).Build();

        var output = RunSP(worker, input);
        Assert.AreEqual(0, output.Count);

        var compress1 = dm.GetJoint(1).compress;

        var edge = n1.Connect(n2, 0);
        input = edge.Build();

        output = RunSP(worker, input);
        Assert.AreEqual(0, output.Count);

        var compress2 = dm.GetJoint(1).compress;

        input = new();
        edge.BreakEdge(input);

        output = RunSP(worker, input);

        var compress3 = dm.GetJoint(1).compress;

        Assert.AreEqual(compress1, compress3);
        Assert.AreNotEqual(compress2, compress3);
        Assert.IsTrue(compress1 > 0);
        Assert.IsTrue(compress2 > 0);

    }


    #region class Limits
    private static class Limits
    {
        public static (float Stretch, float Compress, float Moment)[] Data = new[]
        {
            (5f, 5f, 5f)
        };
    }
    #endregion

    #region class Node
    private class Node
    {
        private readonly SpDataManager dm;
        private readonly int strength;
        private readonly Vector2 force;
        private readonly Vector2 point;
        private readonly int id;
        private readonly int fromId;
        private readonly int fromId2;
        private readonly bool isFixed;
        private List<Node> nodes;


        public Node(SpDataManager dm, Vector2 force = default, bool isFixed = false, Vector2 point = default)
        {
            this.dm = dm;
            this.force = force;
            this.isFixed = isFixed;
            this.point = point;
            id = dm.ReserveNodeIndex();
            nodes = new List<Node>();
            nodes.Add(this);
        }

        private Node(Node other, Vector2 force, bool isFixed, Vector2 point)
        {
            this.dm = other.dm;
            this.force = force;
            this.isFixed = isFixed;
            this.point = point;
            id = dm.ReserveNodeIndex();
            nodes = other.nodes;
            nodes.Add(this);
        }

        private Node(Node other, Vector2 offset, bool isFixed, int strength, Vector2 force)
        {
            dm = other.dm;
            this.isFixed = isFixed;
            this.strength = strength;
            this.force = force;
            point = other.point + offset;
            id = dm.ReserveNodeIndex();
            fromId = other.id;
            nodes = other.nodes;
            nodes.Add(this);
        }

        private Node(Node one, Node two, int strength)
        {
            dm = one.dm;
            nodes = one.nodes;
            nodes.Add(this);
            this.strength = strength;
            fromId = one.id;
            fromId2 = two.id;
        }

        public Node New(Vector2 point, Vector2 force = default, bool isFixed = false)
        {
            return new Node(this, force, isFixed, point);
        }

        public Node Connect(Vector2 offset, int strength, Vector2 force = default, bool isFixed = false)
        {
            return new Node(this, offset, isFixed, strength, force);
        }

        public Node Connect(Node other, int strength)
        {
            if (other.nodes != this.nodes)
            {
                nodes.AddRange(other.nodes);
                foreach (Node node in other.nodes)
                {
                    node.nodes = nodes;
                }
            }
            return new Node(this, other, strength);
        }

        public SpanList<InputCommand> Build()
        {
            SpanList<InputCommand> ret = new();
            foreach (var node in nodes)
            {
                ret.Add(node.BuildOne(ret));
            }
            nodes.Clear();
            return ret;
        }

        private InputCommand BuildOne(SpanList<InputCommand> ret)
        {
            if (id != 0 && fromId == 0)
            {
                return new InputCommand()
                {
                    Command = SpCommand.AddNode,
                    indexA = id,
                    forceA = force,
                    pointA = point,
                    isAFixed = isFixed,
                };
            }
            else if (id != 0 && fromId != 0)
            {
                return new InputCommand()
                {
                    Command = SpCommand.AddNodeAndJoint,
                    indexA = id,
                    forceA = force,
                    pointA = point,
                    isAFixed = isFixed,
                    indexB = fromId,
                    stretchLimit = Limits.Data[strength].Stretch,
                    compressLimit = Limits.Data[strength].Compress,
                    momentLimit = Limits.Data[strength].Moment,
                };
            }
            else if (id == 0 && fromId != 0 && fromId2 != 0)
            {
                return new InputCommand()
                {
                    Command = SpCommand.AddJoint,
                    indexA = fromId,
                    indexB = fromId2,
                    stretchLimit = Limits.Data[strength].Stretch,
                    compressLimit = Limits.Data[strength].Compress,
                    momentLimit = Limits.Data[strength].Moment,
                };
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public void BreakNode(SpanList<InputCommand> ret)
        {
            ret.Add(new InputCommand()
            {
                Command = SpCommand.RemoveNode,
                indexA = id,
            });
        }

        public void BreakEdge(SpanList<InputCommand> ret)
        {
            ret.Add(new InputCommand()
            {
                Command = SpCommand.RemoveJoint,
                indexA = id != 0 ? id : fromId2,
                indexB = fromId
            });

        }
    }
    #endregion
}
