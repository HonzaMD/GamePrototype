using Assets.Scripts.Ai.Jump;
using Assets.Scripts.Map;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Testy hernich sad trajektorii (JumpTrajectories). Novy detektor staci pridat do Detectors.
public class JumpDetectorTests
{
    private static readonly object[] Detectors =
    {
        new object[] { "Small", JumpTrajectories.Get(JumpSet.Small) },
    };

    private struct World : IJumpCellSource
    {
        public Dictionary<Vector2Int, JumpCellClass> Cells;
        public List<Vector2Int> Tested;

        public JumpCellClass Get(Vector2Int cell)
        {
            Tested?.Add(cell);
            return Cells.TryGetValue(cell, out var c) ? c : JumpCellClass.Free;
        }
    }

    private static IEnumerable<JumpTrajectory> All(JumpDetector detector)
    {
        for (int i = 0; i < detector.Count; i++)
            yield return detector.GetTrajectory(i);
    }

    private static bool BruteForce(JumpTrajectory t, Dictionary<Vector2Int, JumpCellClass> cells, Vector2Int origin, int dir)
    {
        foreach (var r in t.Cells)
        {
            var p = new Vector2Int(origin.x + r.Pos.x * dir, origin.y + r.Pos.y);
            if (!r.Accepts(cells.TryGetValue(p, out var c) ? c : JumpCellClass.Free))
                return false;
        }
        return true;
    }

    // Muzou obe trajektorie uspet ve stejnem svete? (zadna spolecna bunka s neslucitelnym pozadavkem)
    private static bool Compatible(JumpTrajectory a, JumpTrajectory b)
    {
        foreach (var ra in a.Cells)
            foreach (var rb in b.Cells)
                if (ra.Pos == rb.Pos && (ra.Accept & rb.Accept) == 0)
                    return false;
        return true;
    }

    [TestCaseSource(nameof(Detectors))]
    public void Stats(string name, JumpDetector detector)
    {
        var automaton = detector.Automaton;
        int cells = All(detector).SelectMany(t => t.Cells).Select(r => r.Pos).Distinct().Count();

        var rnd = new System.Random(3);
        long sum = 0;
        int max = 0;
        const int Worlds = 20000;
        for (int w = 0; w < Worlds; w++)
        {
            var world = new World { Cells = Terrain(rnd), Tested = new List<Vector2Int>() };
            automaton.Run(ref world, Vector2Int.zero, 1);
            sum += world.Tested.Count;
            max = Math.Max(max, world.Tested.Count);
        }

        int maxTests = MaxTests(automaton);
        TestContext.WriteLine($"{name}: trajektorii {detector.Count}, bunek {cells}, stavu {automaton.StateCount} " +
            $"({automaton.StateCount * 12 / 1024} kB), testu na dotaz: nejhure {maxTests}, " +
            $"v terenu prumer {(double)sum / Worlds:F1} max {max}");
        Assert.LessOrEqual(maxTests, cells);
    }

    // Nejdelsi cesta automatem = nejhorsi pocet testovanych bunek na dotaz. Automat je DAG.
    private static int MaxTests(JumpAutomaton automaton)
    {
        var states = automaton.States.ToArray();
        var depth = new int[states.Length];   // 0 = jeste nespocitano

        int Depth(int s)
        {
            if (s < 0)
                return 0;
            if (depth[s] == 0)
            {
                var st = states[s];
                depth[s] = 1 + Math.Max(Depth(st.NextFree), Math.Max(Depth(st.NextStatic), Depth(st.NextOther)));
            }
            return depth[s];
        }

        return states.Length > 0 ? Depth(0) : 0;
    }

    [TestCaseSource(nameof(Detectors))]
    public void EachTrajectory_SucceedsInItsOwnWorld(string name, JumpDetector detector)
    {
        // Svet presne podle pozadavku trajektorie (' ' a 'x' = volno, '#' = staticke) - musi uspet.
        for (int i = 0; i < detector.Count; i++)
        {
            var cells = new Dictionary<Vector2Int, JumpCellClass>();
            foreach (var r in detector.GetTrajectory(i).Cells)
                cells[r.Pos] = r.Accepts(JumpCellClass.Free) ? JumpCellClass.Free : JumpCellClass.Static;

            foreach (int dir in new[] { 1, -1 })
            {
                var mirrored = cells.ToDictionary(p => new Vector2Int(p.Key.x * dir, p.Key.y), p => p.Value);
                var world = new World { Cells = mirrored };
                ulong result = detector.Automaton.Run(ref world, Vector2Int.zero, dir);
                Assert.AreNotEqual(0UL, result & (1UL << i), $"{name}: trajektorie {i} neuspela ve svem svete (dir {dir})");
            }
        }
    }

    [TestCaseSource(nameof(Detectors))]
    public void LandingSpaceIsFree(string name, JumpDetector detector)
    {
        for (int i = 0; i < detector.Count; i++)
        {
            var t = detector.GetTrajectory(i);
            var above = t.Target + Vector2Int.up;
            bool free = t.Cells.Any(r => r.Pos == above && r.Accept == JumpTrajectory.AcceptFree);
            Assert.IsTrue(free, $"{name}: trajektorie {i} nema volno nad cilem {t.Target}");
        }
    }

    [TestCaseSource(nameof(Detectors))]
    public void TargetsAreUnique(string name, JumpDetector detector)
    {
        var dup = All(detector).Select((t, i) => (t.Target, i)).GroupBy(p => p.Target).Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(",", g.Select(p => p.i))}").ToList();
        Assert.IsEmpty(dup, $"{name}: vic trajektorii se stejnym cilem");
    }

    // Kratsi skok ve stejnem radku ma prednost ('x'), takze v jednom radku cilu smi uspet nejvys jedna trajektorie.
    [TestCaseSource(nameof(Detectors))]
    public void AtMostOneTargetPerRow(string name, JumpDetector detector)
    {
        var conflicts = new List<string>();
        for (int i = 0; i < detector.Count; i++)
        {
            for (int j = i + 1; j < detector.Count; j++)
            {
                var a = detector.GetTrajectory(i);
                var b = detector.GetTrajectory(j);
                if (a.Target.y == b.Target.y && Compatible(a, b))
                    conflicts.Add($"{i} {a.Target} + {j} {b.Target}");
            }
        }
        Assert.IsEmpty(conflicts, $"{name}: trajektorie, ktere muzou uspet zaroven ve stejnem radku");
    }

    [TestCaseSource(nameof(Detectors))]
    public void Terrain_MatchesBruteForce(string name, JumpDetector detector)
    {
        var rnd = new System.Random(11);
        var fired = new int[detector.Count];
        for (int w = 0; w < 20000; w++)
        {
            var cells = Terrain(rnd);
            int dir = rnd.Next(2) == 0 ? 1 : -1;
            if (dir < 0)
                cells = cells.ToDictionary(p => new Vector2Int(-p.Key.x, p.Key.y), p => p.Value);

            ulong expected = 0;
            for (int i = 0; i < detector.Count; i++)
            {
                if (BruteForce(detector.GetTrajectory(i), cells, Vector2Int.zero, dir))
                {
                    expected |= 1UL << i;
                    fired[i]++;
                }
            }

            var world = new World { Cells = cells, Tested = new List<Vector2Int>() };
            Assert.AreEqual(expected, detector.Automaton.Run(ref world, Vector2Int.zero, dir), $"{name}: svet {w}");
            Assert.AreEqual(world.Tested.Count, world.Tested.Distinct().Count(), $"{name}: bunka testovana vic nez jednou");
        }

        var never = Enumerable.Range(0, detector.Count).Where(i => fired[i] == 0).ToList();
        Assert.IsEmpty(never, $"{name}: trajektorie, ktere v terenu nikdy neuspely");
    }

    // Teren kolem prisery na [0,0]: sloupce podlahy se schody a dirami, obcas plosinka ve vzduchu a bedna.
    // Prisera stoji na podlaze (0,0), nad ni je volno.
    private static Dictionary<Vector2Int, JumpCellClass> Terrain(System.Random rnd)
    {
        var cells = new Dictionary<Vector2Int, JumpCellClass>();
        int h = 0;
        for (int x = -12; x <= 12; x++)
        {
            if (x != 0 && rnd.Next(3) == 0)
                h = Mathf.Clamp(h + rnd.Next(-4, 5), -4, 4);
            int top = x == 0 ? 0 : h;
            bool hole = x != 0 && rnd.Next(6) == 0;
            if (!hole)
            {
                for (int y = -8; y <= top; y++)
                    cells[new Vector2Int(x, y)] = JumpCellClass.Static;
            }
            if (x == 0)
                h = 0;
            if (rnd.Next(5) == 0)
                cells[new Vector2Int(x, top + rnd.Next(2, 5))] = JumpCellClass.Static;
            if (rnd.Next(10) == 0)
                cells[new Vector2Int(x, top + 1)] = JumpCellClass.Other;
        }
        cells.Remove(new Vector2Int(0, 1));
        cells.Remove(new Vector2Int(0, 2));
        return cells;
    }
}
