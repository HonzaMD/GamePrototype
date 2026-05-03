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
    public SpTests()
    {
        InvariantValidator.EnableInvariantValidator = true;
    }

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

    // Kaskada s BigOnly az do ustaleni - odpovida chovani SpInterface.Runner
    private static SpanList<OutputCommand> RunSPBig(GraphWorker worker, SpanList<InputCommand> input, int maxCascades = 4)
    {
        var forces = new ForceCommand[0];
        var output = new SpanList<OutputCommand>();
        var input2 = new SpanList<InputCommand>();
        worker.ApplyChanges(input.AsSpan(), forces, output);
        for (int i = 0; i < maxCascades; i++)
        {
            worker.GetBrokenEdgesBigOnly(input2, output);
            if (input2.Count == 0) break;
            worker.ApplyChanges(input2.AsSpan(), forces, output);
            input2.Clear();
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


    [Test]
    public void SpTests7ForceSymmetry()
    {
        // Diamond s cyklem (dva roots + cross-link) - nutne pro dvoubarevne cesty.
        // Ukolem: UpdateForce(+F) -> UpdateForce(-F) vrati joint state do puvodni podoby.
        var dm = new SpDataManager();
        var worker = new GraphWorker(dm);

        var r1 = new Node(dm, isFixed: true);
        var n1 = r1.Connect(Vector2.down, 0, force: Vector2.down);
        var n2 = n1.Connect(Vector2.right, 0, force: Vector2.down * 0.5f);
        var r2 = n2.Connect(Vector2.up, 0, isFixed: true);
        r1.Connect(n2, 0); // cross-link
        var input = n1.Build();

        RunSP(worker, input);

        var baseline = new (float compress, float moment)[4];
        for (int i = 0; i < 4; i++)
            baseline[i] = (dm.GetJoint(i).compress, dm.GetJoint(i).moment);

        // Faze 1: zrus sily (node.force += (-F) -> 0)
        var cancel = new SpanList<InputCommand>();
        cancel.Add(new InputCommand { Command = SpCommand.UpdateForce, indexA = n1.Id, forceA = Vector2.up });
        cancel.Add(new InputCommand { Command = SpCommand.UpdateForce, indexA = n2.Id, forceA = Vector2.up * 0.5f });
        RunSP(worker, cancel);

        // Faze 2: obnov sily
        var restore = new SpanList<InputCommand>();
        restore.Add(new InputCommand { Command = SpCommand.UpdateForce, indexA = n1.Id, forceA = Vector2.down });
        restore.Add(new InputCommand { Command = SpCommand.UpdateForce, indexA = n2.Id, forceA = Vector2.down * 0.5f });
        RunSP(worker, restore);

        for (int i = 0; i < 4; i++)
        {
            Assert.AreEqual(baseline[i].compress, dm.GetJoint(i).compress, 1e-4f, $"joint {i} compress");
            Assert.AreEqual(baseline[i].moment, dm.GetJoint(i).moment, 1e-4f, $"joint {i} moment");
        }
    }

    [Test]
    public void SpTests8TempForces()
    {
        var dm = new SpDataManager();
        var worker = new GraphWorker(dm);

        var r = new Node(dm, isFixed: true);
        var n = r.Connect(Vector2.down, 0, force: Vector2.down);
        RunSP(worker, n.Build());

        var permanentCompress = dm.GetJoint(0).compress;
        Assert.AreEqual(0f, dm.GetJoint(0).tempCompress, "temp bez temp forces musi byt 0");

        // Napalim temp force
        var tempForces = new ForceCommand[] { new ForceCommand { indexA = n.Id, forceA = Vector2.down * 3f } };
        var emptyInput = new SpanList<InputCommand>();
        var output = new SpanList<OutputCommand>();
        worker.ApplyChanges(emptyInput.AsSpan(), tempForces, output);

        Assert.AreNotEqual(0f, dm.GetJoint(0).tempCompress, "temp force nebyla propagovana");
        Assert.AreEqual(permanentCompress, dm.GetJoint(0).compress, 1e-4f, "perm compress se nesmi zmenit");

        // GetBrokenEdgesBigOnly vynuluje tempCompress/tempMoment po vyhodnoceni
        var input2 = new SpanList<InputCommand>();
        worker.GetBrokenEdgesBigOnly(input2, output);

        Assert.AreEqual(0f, dm.GetJoint(0).tempCompress, "temp se po BigOnly musi vynulovat");
        Assert.AreEqual(permanentCompress, dm.GetJoint(0).compress, 1e-4f);
    }

    [Test]
    public void SpTests9Cascade()
    {
        // Dve nezavisle vetve z rootu, obe se slabym joitem dole.
        // BigOnly smi v jednom kole ulomit jen nejhorsiho na barvu, druhe kolo ulomi druhou.
        var dm = new SpDataManager();
        var worker = new GraphWorker(dm);

        var r = new Node(dm, isFixed: true);
        var l = r.Connect(Vector2.left, 0);
        var ll = l.Connect(Vector2.down, 2, force: Vector2.down);       // mensi damage
        var right = r.Connect(Vector2.right, 0);
        var rr = right.Connect(Vector2.down, 2, force: Vector2.down * 2f); // vetsi damage
        var input = rr.Build();

        var output = RunSPBig(worker, input);

        var arr = output.AsSpan().ToArray();
        Assert.AreEqual(2, arr.Count(c => c.Command == SpCommand.RemoveJoint), "ocekavam 2 RemoveJoint v kaskade");
        Assert.AreEqual(2, arr.Count(c => c.Command == SpCommand.FallNode), "ocekavam padnuti LL i RR");
        Assert.IsTrue(arr.Any(c => c.Command == SpCommand.FallNode && c.indexA == ll.Id));
        Assert.IsTrue(arr.Any(c => c.Command == SpCommand.FallNode && c.indexA == rr.Id));
    }

    [Test]
    public void SpTests10DualColor()
    {
        // Dva fixed rooty, mezi nimi jeden uzel se silou. Out1 slot musi nest druhou barvu.
        var dm = new SpDataManager();
        var worker = new GraphWorker(dm);

        var r1 = new Node(dm, isFixed: true);
        var n = r1.Connect(Vector2.down, 0, force: Vector2.down);
        var r2 = n.Connect(Vector2.down, 0, isFixed: true);
        RunSP(worker, n.Build());

        var c0 = MathF.Abs(dm.GetJoint(0).compress);
        var c1 = MathF.Abs(dm.GetJoint(1).compress);

        Assert.IsTrue(c0 > 0.1f, "joint 0 musi nest sily od N");
        Assert.IsTrue(c1 > 0.1f, "joint 1 musi nest sily od N");
        Assert.AreEqual(c0, c1, 1e-3f, "sily se rozdeli rovnomerne mezi dva roots");
    }

    [Test]
    public void SpTests11DisjointRoots()
    {
        // Dve disjoint struktury ve stejnem dm - nesmi se ovlivnovat.
        var dm = new SpDataManager();
        var worker = new GraphWorker(dm);

        var r1 = new Node(dm, isFixed: true);
        var n1 = r1.Connect(Vector2.down, 0, force: Vector2.down);
        RunSP(worker, n1.Build());

        var r2 = new Node(dm, isFixed: true, point: Vector2.right * 10f);
        var n2 = r2.Connect(Vector2.down, 0, force: Vector2.down);
        RunSP(worker, n2.Build());

        Assert.AreEqual(1f, MathF.Abs(dm.GetJoint(0).compress), 1e-3f, "joint ve structure 1 nese svoji silu 1");
        Assert.AreEqual(1f, MathF.Abs(dm.GetJoint(1).compress), 1e-3f, "joint ve structure 2 nese svoji silu 1 (ne 2!)");
    }

    [Test]
    public void SpTests12ColorFallback()
    {
        // Uzel visi na dvou fixed roots. Odstranime jednu cestu - musi zustat viset na druhe.
        var dm = new SpDataManager();
        var worker = new GraphWorker(dm);

        var r1 = new Node(dm, isFixed: true);
        var n = r1.Connect(Vector2.down, 0, force: Vector2.down);
        var r2 = n.Connect(Vector2.down, 0, isFixed: true);
        RunSP(worker, n.Build());

        // Zlomim R1-N
        var breakCmd = new SpanList<InputCommand>();
        breakCmd.Add(new InputCommand { Command = SpCommand.RemoveJoint, indexA = r1.Id, indexB = n.Id });
        var output = RunSP(worker, breakCmd);

        var arr = output.AsSpan().ToArray();
        Assert.IsFalse(arr.Any(c => c.Command == SpCommand.FallNode), "N nesmi spadnout, stale visi na R2");
        Assert.AreEqual(1f, MathF.Abs(dm.GetJoint(1).compress), 1e-3f, "zbyly joint N-R2 musi nest plnou silu");
    }

    [Test]
    public void SpTests13LimitsBoundary()
    {
        // compress == compressLimit nesmi prasknout, compress > compressLimit musi.
        // strength 3 => limity (2,2,2), force down * 2 da compress == 2.
        var dmOk = new SpDataManager();
        var workerOk = new GraphWorker(dmOk);
        var rOk = new Node(dmOk, isFixed: true);
        var nOk = rOk.Connect(Vector2.down, 3, force: Vector2.down * 2f);
        var outputOk = RunSP(workerOk, nOk.Build());

        Assert.IsFalse(outputOk.AsSpan().ToArray().Any(c => c.Command == SpCommand.RemoveJoint), "force == limit nesmi prasknout");
        Assert.AreEqual(2f, MathF.Abs(dmOk.GetJoint(0).compress), 1e-4f);

        // Fresh instance, jen nepatrne pres limit
        var dmBreak = new SpDataManager();
        var workerBreak = new GraphWorker(dmBreak);
        var rB = new Node(dmBreak, isFixed: true);
        var nB = rB.Connect(Vector2.down, 3, force: Vector2.down * 2.01f);
        var outputBreak = RunSP(workerBreak, nB.Build());

        Assert.IsTrue(outputBreak.AsSpan().ToArray().Any(c => c.Command == SpCommand.RemoveJoint), "force > limit musi prasknout");
    }

    [Test]
    public void SpTests14ExplicitRemoveJoint()
    {
        // Primy RemoveJoint command bez prekroceni limitu.
        var dm = new SpDataManager();
        var worker = new GraphWorker(dm);

        var r = new Node(dm, isFixed: true);
        var a = r.Connect(Vector2.down, 0);
        var b = a.Connect(Vector2.down, 0, force: Vector2.down);
        RunSP(worker, b.Build());

        var cmd = new SpanList<InputCommand>();
        cmd.Add(new InputCommand { Command = SpCommand.RemoveJoint, indexA = a.Id, indexB = b.Id });
        var output = RunSP(worker, cmd);

        var arr = output.AsSpan().ToArray();
        Assert.IsTrue(arr.Any(c => c.Command == SpCommand.FallNode && c.indexA == b.Id), "B musi spadnout po rozpojeni");
        // A zustava pripojene k root
        Assert.IsFalse(arr.Any(c => c.Command == SpCommand.FallNode && c.indexA == a.Id));
    }

    [Test]
    public void SpTests15SelfLoopThrows()
    {
        var dm = new SpDataManager();
        var worker = new GraphWorker(dm);

        var r = new Node(dm, isFixed: true);
        var n = r.Connect(Vector2.down, 0);
        RunSP(worker, n.Build());

        var cmd = new SpanList<InputCommand>();
        cmd.Add(new InputCommand
        {
            Command = SpCommand.AddJoint,
            indexA = n.Id,
            indexB = n.Id,
            stretchLimit = 5f, compressLimit = 5f, momentLimit = 5f,
        });

        Assert.Throws<InvalidOperationException>(() => RunSP(worker, cmd));
    }

    [Test]
    public void SpTests16NodeIndexRecycle()
    {
        var dm = new SpDataManager();

        // Primy pool test
        int i1 = dm.ReserveNodeIndex();
        int i2 = dm.ReserveNodeIndex();
        Assert.AreNotEqual(i1, i2);
        dm.FreeNodeIndex(i1);
        int i3 = dm.ReserveNodeIndex();
        Assert.AreEqual(i1, i3, "FreeNodeIndex -> Reserve musi vratit stejny index");

        // Round-trip: add -> remove -> free -> reserve -> add (znovu, cisty state)
        var worker = new GraphWorker(dm);
        int iFree = dm.ReserveNodeIndex();
        var add = new SpanList<InputCommand>();
        add.Add(new InputCommand { Command = SpCommand.AddNode, indexA = iFree, isAFixed = true });
        RunSP(worker, add);

        var rm = new SpanList<InputCommand>();
        rm.Add(new InputCommand { Command = SpCommand.RemoveNode, indexA = iFree });
        var output = RunSP(worker, rm);

        Assert.IsTrue(output.AsSpan().ToArray().Any(c => c.Command == SpCommand.FreeNode && c.indexA == iFree));
        dm.FreeNodeIndex(iFree); // to by jinak delal SpInterface.ProcessOutCommands

        int iReused = dm.ReserveNodeIndex();
        Assert.AreEqual(iFree, iReused);

        // Pokud by ClearNode nefungovala, AddNode hodi "Cekal jsem novy node"
        var add2 = new SpanList<InputCommand>();
        add2.Add(new InputCommand { Command = SpCommand.AddNode, indexA = iReused, isAFixed = true });
        Assert.DoesNotThrow(() => RunSP(worker, add2));
    }

    [Test]
    public void SpTests17UpdateJointLimitsShrink()
    {
        // Zmenseni limitu na aktivnim jointu pod jeho aktualni zatez musi vest k prasknuti.
        var dm = new SpDataManager();
        var worker = new GraphWorker(dm);

        var r = new Node(dm, isFixed: true);
        var n = r.Connect(Vector2.down, 0, force: Vector2.down); // limity (5,5,5)
        RunSP(worker, n.Build());

        var compress = MathF.Abs(dm.GetJoint(0).compress);
        Assert.IsTrue(compress > 0.5f && compress < 5f);

        // Sniz limity pod compress
        var shrink = new SpanList<InputCommand>();
        shrink.Add(new InputCommand
        {
            Command = SpCommand.UpdateJointLimits,
            indexA = n.Id, indexB = r.Id,
            stretchLimit = 0.1f, compressLimit = 0.1f, momentLimit = 0.1f,
        });
        var output = RunSP(worker, shrink);

        Assert.IsTrue(output.AsSpan().ToArray().Any(c => c.Command == SpCommand.RemoveJoint), "zmenseny limit musi ulomit");
    }

    [Test]
    public void SpTests18RemoveFixedRoot()
    {
        // Odstraneni fixed rootu odpoji celou vetev.
        var dm = new SpDataManager();
        var worker = new GraphWorker(dm);

        var r = new Node(dm, isFixed: true);
        var a = r.Connect(Vector2.down, 0);
        var b = a.Connect(Vector2.down, 0);
        RunSP(worker, b.Build());

        var rm = new SpanList<InputCommand>();
        rm.Add(new InputCommand { Command = SpCommand.RemoveNode, indexA = r.Id });
        var output = RunSP(worker, rm);

        var arr = output.AsSpan().ToArray();
        Assert.IsTrue(arr.Any(c => c.Command == SpCommand.FreeNode && c.indexA == r.Id), "root ma vratit FreeNode");
        Assert.IsTrue(arr.Any(c => c.Command == SpCommand.FallNode && c.indexA == a.Id), "A musi spadnout");
        Assert.IsTrue(arr.Any(c => c.Command == SpCommand.FallNode && c.indexA == b.Id), "B musi spadnout");
    }

    [Test]
    public void SpTests19SingleFloatingNodeFalls()
    {
        // Jediny AddNode bez isFixed a bez jointu - musi okamzite spadnout.
        var dm = new SpDataManager();
        var worker = new GraphWorker(dm);

        var n = new Node(dm, force: Vector2.down);
        var output = RunSP(worker, n.Build());

        var arr = output.AsSpan().ToArray();
        Assert.IsTrue(arr.Any(c => c.Command == SpCommand.FallNode && c.indexA == n.Id), "osamocely node musi spadnout");
        Assert.IsFalse(arr.Any(c => c.Command == SpCommand.FallEdge), "zadne FallEdge - node nema zadnou hranu");
    }

    [Test]
    public void SpTests20FloatingChainFalls()
    {
        // Retezec 3 propojenych uzlu bez jedineho fixed - cela komponenta musi spadnout,
        // vcetne obou FallEdge (aby se v RB slepily jako tuhe teleso).
        var dm = new SpDataManager();
        var worker = new GraphWorker(dm);

        var a = new Node(dm, force: Vector2.down);
        var b = a.Connect(Vector2.right, 0, force: Vector2.down);
        var c = b.Connect(Vector2.right, 0, force: Vector2.down);
        var output = RunSP(worker, c.Build());

        var arr = output.AsSpan().ToArray();
        Assert.AreEqual(3, arr.Count(x => x.Command == SpCommand.FallNode), "vsechny 3 uzly musi spadnout");
        Assert.AreEqual(2, arr.Count(x => x.Command == SpCommand.FallEdge), "oba jointy se ma prenest do RB pres FallEdge");
    }

    [Test]
    public void SpTests21AddAndRemoveFixedInSameBatch()
    {
        // Pridame fixed + napojeny node + dalsi navazany node, a ve stejnem batchi
        // rovnou fixed root odebereme. Vysledek: fixed ma FreeNode, zbytek spadne.
        var dm = new SpDataManager();
        var worker = new GraphWorker(dm);

        var r = new Node(dm, isFixed: true);
        var a = r.Connect(Vector2.down, 0, force: Vector2.down);
        var b = a.Connect(Vector2.down, 0, force: Vector2.down);

        var input = b.Build();
        r.BreakNode(input); // RemoveNode(r) v tomtez batchi

        var output = RunSP(worker, input);

        var arr = output.AsSpan().ToArray();
        Assert.IsTrue(arr.Any(c => c.Command == SpCommand.FreeNode && c.indexA == r.Id), "r mel dostat FreeNode");
        Assert.IsTrue(arr.Any(c => c.Command == SpCommand.FallNode && c.indexA == a.Id), "a musi spadnout");
        Assert.IsTrue(arr.Any(c => c.Command == SpCommand.FallNode && c.indexA == b.Id), "b musi spadnout");
    }

    [Test]
    public void SpTests22ColorCycleAfterShortcut()
    {
        // Scenar: vyrobime retez R1 - C - B - A "naokolo" (cesta delky 6.5),
        // pak pridame primy edge R1-A jako zkratku (delka 0.5).
        // A.SCD(R1) klesne 6.5 -> 0.5. AddColorWorker na to reaguje rozsirenim
        // R1 z A do B (B.SCD 4 > A.SCD 0.5). Tim se na B.edge_to_A nastavi Out0=R1
        // se smerem k A. Jenze A.edge_to_B.Out0=R1 (stara hodnota retezem) zustane
        // - nikdo ji neinvalidoval - takze obe strany hrany A-B maji Out0=R1.
        // To je cyklus barevneho grafu, ktery by zacyklil ForceWorker.Update.
        var dm = new SpDataManager();
        var worker = new GraphWorker(dm);

        var r1 = new Node(dm, isFixed: true);
        var c  = r1.Connect(Vector2.right * 2f, 0);                       // c = (2,0)
        var b  = c.Connect(Vector2.up * 2f, 0);                           // b = (2,2)
        var a  = b.Connect(new Vector2(-1.5f, -2f), 0);                   // a = (0.5,0)
        RunSP(worker, a.Build());

        // L_R1A = 0.5; chain k A je 2+2+2.5 = 6.5. Zkratka radove kratsi.
        // Pro B: chain 4, pres A 0.5 + 2.5 = 3 -> taky se vyplati -> kaskada.
        var shortcut = r1.Connect(a, 0);
        RunSP(worker, shortcut.Build());

        var aSide = dm.GetOutRoots(a.Id, b.Id);
        var bSide = dm.GetOutRoots(b.Id, a.Id);

        bool aSideHasR1 = aSide.Out0Root == r1.Id || aSide.Out1Root == r1.Id;
        bool bSideHasR1 = bSide.Out0Root == r1.Id || bSide.Out1Root == r1.Id;

        Assert.IsFalse(aSideHasR1 && bSideHasR1,
            $"Barevny cyklus: hrana A-B nese R1 v obou smerech. " +
            $"A.edge_to_B.Out=({aSide.Out0Root},{aSide.Out1Root}); " +
            $"B.edge_to_A.Out=({bSide.Out0Root},{bSide.Out1Root})");
    }

    // ====================================================================
    // toUpdate correctness tests
    //
    // ForceWorker.RemoveForces (stara topologie) + AddForces (nova topologie)
    // funguje korektne jen kdyz toUpdate obsahuje VSECHNY uzly, jejichz
    // force-routing se mezi starou a novou topologii zmenil. To zahrnuje
    // listy hluboko pod modifikovanou hranou - jejich routing zavisi na
    // junction profilech a delkach/pevnostech podel cele cesty k rootu.
    //
    // Strategie: postavit stejnou cilovou topologii dvema zpusoby:
    //   (a) "fresh" v jednom batchi, (b) "incremental" - zaklad pak modifikace.
    // Joint compress/moment hodnoty MUSI byt identicke. Lisi-li se, toUpdate
    // ma diru.
    //
    // Predpoklad: oba scenare volaji ReserveNodeIndex / Build ve stejnem poradi,
    // takze node IDs i joint indexy se shoduji.
    // ====================================================================

    private static void AssertJointEquals(SpDataManager fresh, SpDataManager incr, int from, int to, string label)
    {
        int idxFresh = fresh.GetJointIndex(from, to);
        int idxIncr = incr.GetJointIndex(from, to);
        var jf = fresh.GetJoint(idxFresh);
        var ji = incr.GetJoint(idxIncr);
        Assert.AreEqual(jf.compress, ji.compress, 1e-3f, $"{label} compress (fresh={jf.compress}, incr={ji.compress})");
        Assert.AreEqual(jf.moment, ji.moment, 1e-3f, $"{label} moment (fresh={jf.moment}, incr={ji.moment})");
    }

    [Test]
    public void SpTests23ShortcutFreshVsIncremental()
    {
        // Pridanim zkratky upstream se meni rozlozeni sily v listu n3 (visi pod n2).
        // n3 nema modifikovanou zadnou svou hranu - musi byt v toUpdate jen kvuli zmene
        // junction profilu na n2 (n2 dostalo druhou cestu k rootu).

        // (a) fresh
        var dmF = new SpDataManager();
        var wF = new GraphWorker(dmF);
        var rF = new Node(dmF, isFixed: true);
        var n1F = rF.Connect(Vector2.down, 0);
        var n2F = n1F.Connect(Vector2.down, 0);
        var n3F = n2F.Connect(Vector2.down, 0, force: Vector2.down);
        rF.Connect(n2F, 0); // zkratka soucasti fresh stavby
        RunSP(wF, n3F.Build());

        // (b) incremental: nejprve retez, pak zkratka
        var dmI = new SpDataManager();
        var wI = new GraphWorker(dmI);
        var rI = new Node(dmI, isFixed: true);
        var n1I = rI.Connect(Vector2.down, 0);
        var n2I = n1I.Connect(Vector2.down, 0);
        var n3I = n2I.Connect(Vector2.down, 0, force: Vector2.down);
        RunSP(wI, n3I.Build());
        var sc = rI.Connect(n2I, 0);
        RunSP(wI, sc.Build());

        Assert.AreEqual(rF.Id, rI.Id);  // sanity: node IDs match
        Assert.AreEqual(n3F.Id, n3I.Id);

        AssertJointEquals(dmF, dmI, rF.Id, n1F.Id, "R-n1");
        AssertJointEquals(dmF, dmI, n1F.Id, n2F.Id, "n1-n2");
        AssertJointEquals(dmF, dmI, n2F.Id, n3F.Id, "n2-n3");
        AssertJointEquals(dmF, dmI, rF.Id, n2F.Id, "R-n2 (zkratka)");
    }

    [Test]
    public void SpTests24SecondRootFreshVsIncremental()
    {
        // Pridani druheho fixed rootu vyrobi novou barvu. List n2 mel jen jednu barvu;
        // po pridani R2 ma dve a jeho FindOtherColor pri force propagaci dela split.
        // n2 musi byt v toUpdate s novym color profilem.

        // (a) fresh
        var dmF = new SpDataManager();
        var wF = new GraphWorker(dmF);
        var r1F = new Node(dmF, isFixed: true);
        var n1F = r1F.Connect(Vector2.down, 0);
        var n2F = n1F.Connect(Vector2.down, 0, force: Vector2.down);
        var r2F = n1F.Connect(Vector2.up, 0, isFixed: true);
        RunSP(wF, n2F.Build());

        // (b) incremental: nejprve jen R1-n1-n2 retez se silou, pak teprve R2
        var dmI = new SpDataManager();
        var wI = new GraphWorker(dmI);
        var r1I = new Node(dmI, isFixed: true);
        var n1I = r1I.Connect(Vector2.down, 0);
        var n2I = n1I.Connect(Vector2.down, 0, force: Vector2.down);
        RunSP(wI, n2I.Build());
        var r2I = n1I.Connect(Vector2.up, 0, isFixed: true);
        RunSP(wI, r2I.Build());

        AssertJointEquals(dmF, dmI, r1F.Id, n1F.Id, "R1-n1");
        AssertJointEquals(dmF, dmI, n1F.Id, n2F.Id, "n1-n2");
        AssertJointEquals(dmF, dmI, r2F.Id, n1F.Id, "R2-n1 (novy root)");
    }

    [Test]
    public void SpTests25StrengthChangeFreshVsIncremental()
    {
        // Dve paralelni cesty z n2 k R1 - jedna primo (R1-n2 se strong limity),
        // druha pres n1 (boundary str=3, limit 2). Pevnost pres silnejsi cestu by mela
        // posunout vahu do silnejsi vetve. Pri inkrementalnim pridani silne zkratky
        // musi byt n3 (list se silou pod n2) v toUpdate aby se sila prerozdelila.
        // Pozn: nelze pouzit str=2 (limit 0.5) protoze v round 1 incrementalu by retez
        // s plnou silou prasknul jeste pred pridanim zkratky.

        // (a) fresh - silna zkratka R1-n2 (strength 1, limit 100) + R1-n1-n2 (strength 3, limit 2)
        var dmF = new SpDataManager();
        var wF = new GraphWorker(dmF);
        var rF = new Node(dmF, isFixed: true);
        var n1F = rF.Connect(Vector2.down, 3);
        var n2F = n1F.Connect(Vector2.down, 3);
        var n3F = n2F.Connect(Vector2.down, 0, force: Vector2.down);
        rF.Connect(n2F, 1);                                        // silna zkratka
        RunSP(wF, n3F.Build());

        // (b) incremental: stejny zaklad, pak pridat zkratku
        var dmI = new SpDataManager();
        var wI = new GraphWorker(dmI);
        var rI = new Node(dmI, isFixed: true);
        var n1I = rI.Connect(Vector2.down, 3);
        var n2I = n1I.Connect(Vector2.down, 3);
        var n3I = n2I.Connect(Vector2.down, 0, force: Vector2.down);
        RunSP(wI, n3I.Build());
        var sc = rI.Connect(n2I, 1);
        RunSP(wI, sc.Build());

        AssertJointEquals(dmF, dmI, rF.Id, n1F.Id, "R-n1");
        AssertJointEquals(dmF, dmI, n1F.Id, n2F.Id, "n1-n2");
        AssertJointEquals(dmF, dmI, n2F.Id, n3F.Id, "n2-n3");
        AssertJointEquals(dmF, dmI, rF.Id, n2F.Id, "R-n2 silna zkratka");
    }

    [Test]
    public void SpTests26DeepLeafShortcutFreshVsIncremental()
    {
        // Hlubsi retez: zkratka u n2, ale list se silou je n4 (2 hopy pod n2).
        // n4 nema modifikovanou zadnou hranu, a presto jeho force routing zavisi
        // na novem junction profilu n2.

        // (a) fresh
        var dmF = new SpDataManager();
        var wF = new GraphWorker(dmF);
        var rF = new Node(dmF, isFixed: true);
        var n1F = rF.Connect(Vector2.down, 0);
        var n2F = n1F.Connect(Vector2.down, 0);
        var n3F = n2F.Connect(Vector2.down, 0);
        var n4F = n3F.Connect(Vector2.down, 0, force: Vector2.down);
        rF.Connect(n2F, 0); // zkratka R-n2
        RunSP(wF, n4F.Build());

        // (b) incremental
        var dmI = new SpDataManager();
        var wI = new GraphWorker(dmI);
        var rI = new Node(dmI, isFixed: true);
        var n1I = rI.Connect(Vector2.down, 0);
        var n2I = n1I.Connect(Vector2.down, 0);
        var n3I = n2I.Connect(Vector2.down, 0);
        var n4I = n3I.Connect(Vector2.down, 0, force: Vector2.down);
        RunSP(wI, n4I.Build());
        var sc = rI.Connect(n2I, 0);
        RunSP(wI, sc.Build());

        AssertJointEquals(dmF, dmI, rF.Id, n1F.Id, "R-n1");
        AssertJointEquals(dmF, dmI, n1F.Id, n2F.Id, "n1-n2");
        AssertJointEquals(dmF, dmI, n2F.Id, n3F.Id, "n2-n3");
        AssertJointEquals(dmF, dmI, n3F.Id, n4F.Id, "n3-n4");
        AssertJointEquals(dmF, dmI, rF.Id, n2F.Id, "R-n2 zkratka");
    }

    [Test]
    public void SpTests27OrphanedColorOverwrite()
    {
        // Krok 1: dva rooty R1, R2 oba na N3, retez N3-N4-N5-N6.
        //  N5.SCD(R1) = N5.SCD(R2) = 3. Hrana N5->N4 nese Out0=R1, Out1=R2 (oboji len 3).
        //  Hrana N5-N6 ma In0=R1, In1=R2 (mirror N6.Out0=R1, Out1=R2 - R1/R2 cesta z N6 vede pres N5).
        //
        // Krok 2: pridame R3 napojeny na N4 a N6 (oboji hrana ~sqrt(2)).
        //  AddColorWorker expanduje R3 z N4 do N5 s lengthB ~2.41, coz je lepsi nez Out1=R2 (3).
        //  V else-if vetvi (AddColorWorker.cs ~r158) prepiseme Out1Root=R2 -> R3 a oznacime
        //  (N5, R2) jako dirty.
        //  Jenze hrana N5-N6 stale ma In1Root=R2 - osyrela vstupni hrana, R2 do N5 vchazi
        //  ale uz nikam nevychazi.
        //  ConsistencyWorker pak nad (N5, R2) napocita scd=MaxValue, ale prochazi In1Root=R2
        //  hranu -> Assert "hrana bez korenu" failne (ConsistencyWorker.cs:143).

        var dm = new SpDataManager();
        var worker = new GraphWorker(dm);

        var r1 = new Node(dm, isFixed: true);                             // (0, 0)
        var n3 = r1.Connect(Vector2.right, 0);                            // (1, 0)
        var r2 = new Node(dm, isFixed: true, point: Vector2.right * 2f);  // (2, 0)
        r2.Connect(n3, 0);
        var n4 = n3.Connect(Vector2.down, 0);                             // (1, -1)
        var n5 = n4.Connect(Vector2.down, 0);                             // (1, -2)
        var n6 = n5.Connect(Vector2.down, 0);                             // (1, -3)
        RunSP(worker, n6.Build());

        var r3 = new Node(dm, isFixed: true, point: new Vector2(2f, -2f)); // (2, -2)
        r3.Connect(n4, 0);                                                 // sqrt(2)
        var edge = r3.Connect(n6, 0);                                      // sqrt(2)
        RunSP(worker, edge.Build());
    }

    [Test]
    public void SpTests28RandomTopology()
    {
        // Deterministicky nahodne vystaveny graf: 20 fixed roots + 80 regularnich uzlu, 120 hran.
        // Pak 50 nahodnych modifikacnich kroku ze vsech druhu (Add/Remove Node/Joint, UpdateForce/Limits).
        // Test sleduje, ze ApplyChanges + InvariantValidator nezasertuje na zadnem kroku.
        const int seed = 12345;
        const int rootCount = 20;
        const int totalNodes = 100;
        const int targetEdges = 120;
        const int modSteps = 50;

        var rng = new System.Random(seed);
        var dm = new SpDataManager();
        var worker = new GraphWorker(dm);

        var alive = new HashSet<int>();
        var fixedNodes = new HashSet<int>();
        var nodeForce = new Dictionary<int, Vector2>();
        var edgesSet = new HashSet<(int, int)>();
        var neighbors = new Dictionary<int, HashSet<int>>();

        Vector2 RandomPoint() => new Vector2((float)(rng.NextDouble() * 50.0 - 25.0), (float)(rng.NextDouble() * 50.0 - 25.0));
        Vector2 RandomForce(double scale) => new Vector2((float)((rng.NextDouble() - 0.5) * 2 * scale), (float)((rng.NextDouble() - 0.5) * 2 * scale));
        int RandomStrength() => rng.Next(0, Limits.Data.Length);
        (int, int) Norm(int a, int b) => a < b ? (a, b) : (b, a);
        int PickAlive() => alive.ElementAt(rng.Next(alive.Count));
        bool TryPickEdge(out int a, out int b)
        {
            if (edgesSet.Count == 0) { a = b = 0; return false; }
            var e = edgesSet.ElementAt(rng.Next(edgesSet.Count));
            a = e.Item1; b = e.Item2;
            return true;
        }

        void RegisterAdd(int id, Vector2 force, bool isFixed)
        {
            alive.Add(id);
            if (isFixed) fixedNodes.Add(id);
            nodeForce[id] = force;
            neighbors[id] = new HashSet<int>();
        }

        void RegisterEdge(int a, int b)
        {
            edgesSet.Add(Norm(a, b));
            neighbors[a].Add(b);
            neighbors[b].Add(a);
        }

        void DropNode(int id)
        {
            if (!alive.Remove(id)) return;
            fixedNodes.Remove(id);
            if (neighbors.TryGetValue(id, out var nbs))
            {
                foreach (var nb in nbs)
                {
                    edgesSet.Remove(Norm(id, nb));
                    if (neighbors.TryGetValue(nb, out var nbnbs))
                        nbnbs.Remove(id);
                }
                neighbors.Remove(id);
            }
            nodeForce.Remove(id);
            dm.FreeNodeIndex(id);
        }

        void DropEdge(int a, int b)
        {
            if (!edgesSet.Remove(Norm(a, b))) return;
            if (neighbors.TryGetValue(a, out var na)) na.Remove(b);
            if (neighbors.TryGetValue(b, out var nb)) nb.Remove(a);
        }

        void ProcessOutput(SpanList<OutputCommand> output)
        {
            var span = output.AsSpan();
            for (int i = 0; i < span.Length; i++)
            {
                var cmd = span[i];
                if (cmd.Command == SpCommand.FallNode || cmd.Command == SpCommand.FreeNode)
                    DropNode(cmd.indexA);
                else if (cmd.Command == SpCommand.RemoveJoint)
                    DropEdge(cmd.indexA, cmd.indexB);
            }
        }

        SpanList<OutputCommand> RunSettle(SpanList<InputCommand> input, int maxCascades = 10)
        {
            var forces = new ForceCommand[0];
            var output = new SpanList<OutputCommand>();
            var input2 = new SpanList<InputCommand>();
            worker.ApplyChanges(input.AsSpan(), forces, output);
            for (int i = 0; i < maxCascades; i++)
            {
                worker.GetBrokenEdgesBigOnly(input2, output);
                if (input2.Count == 0) break;
                worker.ApplyChanges(input2.AsSpan(), forces, output);
                input2.Clear();
            }
            return output;
        }

        // --- Initial build: 20 fixed roots ---
        var input = new SpanList<InputCommand>();
        for (int i = 0; i < rootCount; i++)
        {
            int id = dm.ReserveNodeIndex();
            input.Add(new InputCommand { Command = SpCommand.AddNode, indexA = id, pointA = RandomPoint(), isAFixed = true });
            RegisterAdd(id, Vector2.zero, true);
        }

        // 80 regularnich uzlu, kazdy se napojuje na nahodny existujici alive node => zaruci pripojeni ke kostre
        for (int i = 0; i < totalNodes - rootCount; i++)
        {
            int parent = PickAlive();
            int id = dm.ReserveNodeIndex();
            var force = rng.NextDouble() < 0.3 ? RandomForce(1.0) : Vector2.zero;
            int s = RandomStrength();
            var lim = Limits.Data[s];
            input.Add(new InputCommand
            {
                Command = SpCommand.AddNodeAndJoint,
                indexA = id, pointA = RandomPoint(), forceA = force, indexB = parent,
                stretchLimit = lim.Stretch, compressLimit = lim.Compress, momentLimit = lim.Moment,
            });
            RegisterAdd(id, force, false);
            RegisterEdge(id, parent);
        }

        // Doplnit cross-hrany az do targetEdges
        int attempts = 0;
        while (edgesSet.Count < targetEdges && attempts < targetEdges * 20)
        {
            attempts++;
            int a = PickAlive(), b = PickAlive();
            if (a == b) continue;
            if (edgesSet.Contains(Norm(a, b))) continue;
            int s = RandomStrength();
            var lim = Limits.Data[s];
            input.Add(new InputCommand
            {
                Command = SpCommand.AddJoint,
                indexA = a, indexB = b,
                stretchLimit = lim.Stretch, compressLimit = lim.Compress, momentLimit = lim.Moment,
            });
            RegisterEdge(a, b);
        }

        Assert.AreEqual(targetEdges, edgesSet.Count, "build: ocekavam plny pocet hran");

        var output0 = RunSettle(input);
        ProcessOutput(output0);

        // --- 50 modifikacnich kroku ---
        for (int step = 0; step < modSteps; step++)
        {
            input = new SpanList<InputCommand>();
            int kind = rng.Next(7);

            switch (kind)
            {
                case 0: // AddNode (samostatny - obvykle spadne, fixed roots zustanou)
                {
                    int id = dm.ReserveNodeIndex();
                    bool fix = rng.NextDouble() < 0.3;
                    var force = rng.NextDouble() < 0.5 ? RandomForce(1.0) : Vector2.zero;
                    input.Add(new InputCommand { Command = SpCommand.AddNode, indexA = id, pointA = RandomPoint(), isAFixed = fix, forceA = force });
                    RegisterAdd(id, force, fix);
                    break;
                }
                case 1: // AddNodeAndJoint
                {
                    if (alive.Count == 0) break;
                    int parent = PickAlive();
                    int id = dm.ReserveNodeIndex();
                    var force = rng.NextDouble() < 0.4 ? RandomForce(1.0) : Vector2.zero;
                    int s = RandomStrength();
                    var lim = Limits.Data[s];
                    input.Add(new InputCommand
                    {
                        Command = SpCommand.AddNodeAndJoint,
                        indexA = id, pointA = RandomPoint(), forceA = force, indexB = parent,
                        stretchLimit = lim.Stretch, compressLimit = lim.Compress, momentLimit = lim.Moment,
                    });
                    RegisterAdd(id, force, false);
                    RegisterEdge(id, parent);
                    break;
                }
                case 2: // AddJoint mezi dvema alive nody (bez self-loop a duplicit)
                {
                    if (alive.Count < 2) break;
                    int a = PickAlive(), b = PickAlive();
                    if (a == b) break;
                    if (edgesSet.Contains(Norm(a, b))) break;
                    int s = RandomStrength();
                    var lim = Limits.Data[s];
                    input.Add(new InputCommand
                    {
                        Command = SpCommand.AddJoint,
                        indexA = a, indexB = b,
                        stretchLimit = lim.Stretch, compressLimit = lim.Compress, momentLimit = lim.Moment,
                    });
                    RegisterEdge(a, b);
                    break;
                }
                case 3: // RemoveJoint
                {
                    if (!TryPickEdge(out int a, out int b)) break;
                    input.Add(new InputCommand { Command = SpCommand.RemoveJoint, indexA = a, indexB = b });
                    DropEdge(a, b);
                    break;
                }
                case 4: // RemoveNode (pripadne vc. fixed rootu - spousti kaskady padu)
                {
                    if (alive.Count == 0) break;
                    int id = PickAlive();
                    input.Add(new InputCommand { Command = SpCommand.RemoveNode, indexA = id });
                    // tracking se srovna v ProcessOutput pres FreeNode/FallNode
                    break;
                }
                case 5: // UpdateForce - relativni delta
                {
                    if (alive.Count == 0) break;
                    int id = PickAlive();
                    var delta = RandomForce(0.5);
                    input.Add(new InputCommand { Command = SpCommand.UpdateForce, indexA = id, forceA = delta });
                    if (nodeForce.ContainsKey(id))
                        nodeForce[id] += delta;
                    break;
                }
                case 6: // UpdateJointLimits
                {
                    if (!TryPickEdge(out int a, out int b)) break;
                    int s = RandomStrength();
                    var lim = Limits.Data[s];
                    input.Add(new InputCommand
                    {
                        Command = SpCommand.UpdateJointLimits,
                        indexA = a, indexB = b,
                        stretchLimit = lim.Stretch, compressLimit = lim.Compress, momentLimit = lim.Moment,
                    });
                    break;
                }
            }

            if (input.Count == 0) continue;

            var stepOutput = RunSettle(input);
            ProcessOutput(stepOutput);
        }

        // Sanity: aspon fixed roots by mely prezit (RemoveNode na ne dopada zridka pri 50 krocich)
        Assert.IsTrue(alive.Count > 0, "po vsech krocich nezbyl ani jeden zivy uzel");
    }

    #region class Limits
    private static class Limits
    {
        public static (float Stretch, float Compress, float Moment)[] Data = new[]
        {
            (5f, 5f, 5f),        // 0: default
            (100f, 100f, 100f),  // 1: strong
            (0.5f, 0.5f, 0.5f),  // 2: weak
            (2f, 2f, 2f),        // 3: boundary
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
        public int Id => id;
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
