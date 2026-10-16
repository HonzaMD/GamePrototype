using Assets.Scripts.Ai.Jump;
using Assets.Scripts.Map;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class JumpAutomatonTests
{
    // Svet zadany ASCII (horni radek = nejvyssi y): '#' a '*' = staticke, 'o' = pohyblive, ' ' = volno.
    // Mimo zadany obdelnik je volno. Pamatuje si testovane bunky.
    private struct AsciiWorld : IJumpCellSource
    {
        private readonly string[] rows;
        public readonly Vector2Int Origin;
        public readonly List<Vector2Int> Tested;

        public AsciiWorld(params string[] rows)
        {
            this.rows = rows;
            Origin = default;
            Tested = new List<Vector2Int>();
            for (int r = 0; r < rows.Length; r++)
            {
                int c = rows[r].IndexOf('*');
                if (c >= 0)
                    Origin = new Vector2Int(c, rows.Length - 1 - r);
            }
        }

        public JumpCellClass Get(Vector2Int cell)
        {
            Tested.Add(cell);
            int r = rows.Length - 1 - cell.y;
            if (r < 0 || r >= rows.Length || cell.x < 0 || cell.x >= rows[r].Length)
                return JumpCellClass.Free;
            return rows[r][cell.x] switch
            {
                '#' or '*' => JumpCellClass.Static,
                'o' => JumpCellClass.Other,
                _ => JumpCellClass.Free,
            };
        }
    }

    private struct RandomWorld : IJumpCellSource
    {
        private readonly Dictionary<Vector2Int, JumpCellClass> cells;
        public readonly List<Vector2Int> Tested;

        public RandomWorld(Dictionary<Vector2Int, JumpCellClass> cells)
        {
            this.cells = cells;
            Tested = new List<Vector2Int>();
        }

        public JumpCellClass Get(Vector2Int cell)
        {
            Tested.Add(cell);
            return cells.TryGetValue(cell, out var c) ? c : JumpCellClass.Free;
        }
    }

    private static ulong Run(JumpDetector detector, AsciiWorld world, int dir = 1)
    {
        var result = detector.Automaton.Run(ref world, world.Origin, dir);
        AssertNoDuplicates(world.Tested);
        return result;
    }

    private static void AssertNoDuplicates(List<Vector2Int> tested)
    {
        Assert.AreEqual(tested.Count, new HashSet<Vector2Int>(tested).Count, "Bunka testovana vic nez jednou");
    }

    // Mala testovaci sada pro telo v jedne bunce (nezavisla na hernich sadach v JumpTrajectories).
    private const string MapStepUp1 = @"
        |  |
        |?#|
        |*?|";

    private const string MapStepUp2 = @"
        |  |
        | #|
        |??|
        |*?|";

    private const string MapGap1 = @"
        |   |
        |?  |
        |*x#|";

    private const string MapGap2 = @"
        |    |
        |    |
        |?   |
        |*xx#|";

    private static readonly JumpDetector small = new JumpDetector(MapStepUp1, MapStepUp2, MapGap1, MapGap2);
    private const ulong MStepUp1 = 1, MStepUp2 = 2, MGap1 = 4, MGap2 = 8;   // bity v masce vysledku

    [Test]
    public void Parse_OffsetsAndTarget()
    {
        var t = JumpTrajectory.Parse(MapGap1);
        Assert.AreEqual(new Vector2Int(2, 0), t.Target);
        Assert.AreEqual(7, t.Cells.Length);

        var x = Array.Find(t.Cells, r => r.Pos == new Vector2Int(1, 0));
        Assert.AreEqual(JumpTrajectory.AcceptNotStatic, x.Accept);
        var free = Array.Find(t.Cells, r => r.Pos == new Vector2Int(0, 2));
        Assert.AreEqual(JumpTrajectory.AcceptFree, free.Accept);
        Assert.IsFalse(Array.Exists(t.Cells, r => r.Pos == new Vector2Int(0, 1)), "'?' se netestuje");
    }

    [Test]
    public void Parse_Errors()
    {
        Assert.Throws<FormatException>(() => JumpTrajectory.Parse("| |\n|* |"));      // ruzna sirka
        Assert.Throws<FormatException>(() => JumpTrajectory.Parse("|  |\n|* |"));     // chybi '#'
        Assert.Throws<FormatException>(() => JumpTrajectory.Parse("|##|\n|* |"));     // dva '#'
        Assert.Throws<FormatException>(() => JumpTrajectory.Parse("| #|\n|*a|"));     // neznamy znak
        Assert.Throws<FormatException>(() => JumpTrajectory.Parse("| #\n|* |"));      // chybi koncove '|'
    }

    [Test]
    public void StepUp1()
    {
        var w = new AsciiWorld(
            "     ",
            "  ## ",
            " *###");
        Assert.AreEqual(MStepUp1, Run(small, w));
    }

    [Test]
    public void StepUp2_WallTwoHigh()
    {
        var w = new AsciiWorld(
            "     ",
            "  #  ",
            "  #  ",
            " *#  ");
        Assert.AreEqual(MStepUp2, Run(small, w));
    }

    [Test]
    public void Gaps_OnlyNearestInDirection()
    {
        // Gap2 ma 'x' na (2,0): kdyz je tam doskociste, vyhraje kratsi Gap1.
        var w = new AsciiWorld(
            "      ",
            "      ",
            "      ",
            "*  ###");
        Assert.AreEqual(MGap2, Run(small, w));

        var w2 = new AsciiWorld(
            "      ",
            "      ",
            "      ",
            "* ####");
        Assert.AreEqual(MGap1, Run(small, w2));

        var flat = new AsciiWorld(
            "      ",
            "      ",
            "      ",
            "*#####");
        Assert.AreEqual(0UL, Run(small, flat), "Po rovine se neskace");
    }

    [Test]
    public void Mirror()
    {
        var w = new AsciiWorld(
            "      ",
            "      ",
            "      ",
            "### *");
        Assert.AreEqual(MGap1, Run(small, w, -1));
        Assert.AreEqual(new Vector2Int(-2, 0), small.TargetOffset(2, -1));
    }

    [Test]
    public void MovingObjectBlocksPath()
    {
        var w = new AsciiWorld(
            "      ",
            "  o   ",
            "      ",
            "* ####");
        Assert.AreEqual(0UL, Run(small, w));
    }

    [Test]
    public void LowCeiling_StopsEarly()
    {
        // Volno nad hlavou (0,2) potrebuji vsechny trajektorie -> po nem se dal netestuje.
        // Poradi: (1,0), (1,1), (2,0), (0,2).
        var w = new AsciiWorld(
            "#######",
            "       ",
            "*  ####");
        var result = small.Automaton.Run(ref w, w.Origin, 1);
        Assert.AreEqual(0UL, result);
        Assert.AreEqual(new Vector2Int(0, 2), w.Tested[w.Tested.Count - 1]);
        Assert.LessOrEqual(w.Tested.Count, 4);
    }

    [Test]
    public void Random_MatchesBruteForce()
    {
        var rnd = new System.Random(12345);
        var accepts = new[] { JumpTrajectory.AcceptFree, JumpTrajectory.AcceptStatic, JumpTrajectory.AcceptNotStatic };
        int hits = 0;

        for (int iter = 0; iter < 200; iter++)
        {
            // Nezavisle nahodne trajektorie jsou pro automat nejhorsi pripad - proto jich je malo.
            int n = rnd.Next(1, 13);
            var trajectories = new JumpTrajectory[n];
            for (int i = 0; i < n; i++)
            {
                var used = new HashSet<Vector2Int>();
                var reqs = new List<JumpTrajectory.Requirement>();
                int m = rnd.Next(1, 10);
                for (int j = 0; j < m; j++)
                {
                    var pos = new Vector2Int(rnd.Next(-1, 6), rnd.Next(-3, 4));
                    if (used.Add(pos))
                        reqs.Add(new JumpTrajectory.Requirement(pos, accepts[rnd.Next(accepts.Length)]));
                }
                trajectories[i] = new JumpTrajectory(reqs[0].Pos, reqs.ToArray());
            }
            hits += CheckAgainstBruteForce(trajectories, rnd, 50, 0.5, $"iter {iter}");
        }
        Assert.Greater(hits, 1000);
    }

    [Test]
    public void RealisticSet_MatchesBruteForce()
    {
        var trajectories = RealisticSet(6);
        var automaton = JumpAutomaton.Build(trajectories);
        Assert.LessOrEqual(automaton.StateCount, 12000, "Realna sada nesmi vybuchnout");
        int hits = CheckAgainstBruteForce(trajectories, new System.Random(7), 2000, 0.15, "realistic");
        Assert.Greater(hits, 100);
    }

    // Skoky doprava pro telo 1 bunka: nahoru, vodorovne, dolu na cil (tx, ty); 'x' na kratsim doskoku v radku.
    private static JumpTrajectory[] RealisticSet(int maxX)
    {
        var list = new List<JumpTrajectory>();
        for (int tx = 1; tx <= maxX; tx++)
        {
            for (int ty = -3; ty <= 3; ty++)
            {
                var req = new Dictionary<Vector2Int, byte>();
                int top = Math.Max(ty, 0) + 2;
                for (int y = 2; y <= top; y++)
                    req[new Vector2Int(0, y)] = JumpTrajectory.AcceptFree;
                for (int x = 0; x <= tx; x++)
                    req[new Vector2Int(x, top)] = JumpTrajectory.AcceptFree;
                for (int y = ty + 1; y < top; y++)
                    req[new Vector2Int(tx, y)] = JumpTrajectory.AcceptFree;
                req[new Vector2Int(tx, ty)] = JumpTrajectory.AcceptStatic;
                if (tx > 1)
                    req[new Vector2Int(tx - 1, ty)] = JumpTrajectory.AcceptNotStatic;
                req.Remove(Vector2Int.zero);
                list.Add(new JumpTrajectory(new Vector2Int(tx, ty),
                    req.Select(p => new JumpTrajectory.Requirement(p.Key, p.Value)).ToArray()));
            }
        }
        return list.ToArray();
    }

    // Vraci pocet svetu, kde slo skocit (kontrola, ze test neco testuje).
    private static int CheckAgainstBruteForce(JumpTrajectory[] trajectories, System.Random rnd, int worlds, double density, string label)
    {
        var automaton = JumpAutomaton.Build(trajectories);
        int hits = 0;
        for (int w = 0; w < worlds; w++)
        {
            var cells = new Dictionary<Vector2Int, JumpCellClass>();
            for (int x = -8; x <= 8; x++)
            {
                for (int y = -5; y <= 5; y++)
                {
                    if (rnd.NextDouble() < density)
                        cells[new Vector2Int(x, y)] = rnd.Next(4) == 0 ? JumpCellClass.Other : JumpCellClass.Static;
                }
            }
            var origin = new Vector2Int(rnd.Next(-1, 2), rnd.Next(-1, 2));
            int dir = rnd.Next(2) == 0 ? 1 : -1;

            ulong expected = 0;
            for (int i = 0; i < trajectories.Length; i++)
            {
                bool ok = true;
                foreach (var r in trajectories[i].Cells)
                {
                    var p = new Vector2Int(origin.x + r.Pos.x * dir, origin.y + r.Pos.y);
                    ok &= r.Accepts(cells.TryGetValue(p, out var c) ? c : JumpCellClass.Free);
                }
                if (ok)
                    expected |= 1UL << i;
            }
            if (expected != 0)
                hits++;

            var world = new RandomWorld(cells);
            Assert.AreEqual(expected, automaton.Run(ref world, origin, dir), $"{label}, world {w}");
            AssertNoDuplicates(world.Tested);
        }
        return hits;
    }

    [Test]
    public void SharedLastCell_EmitsAllAtOnce()
    {
        // Obe trajektorie konci na stejne bunce -> prechod prida dve trajektorie najednou (tabulka emits).
        var a = new JumpTrajectory(new Vector2Int(1, 0), new[] { new JumpTrajectory.Requirement(new Vector2Int(1, 0), JumpTrajectory.AcceptStatic) });
        var b = new JumpTrajectory(new Vector2Int(1, 0), new[] {
            new JumpTrajectory.Requirement(new Vector2Int(0, 1), JumpTrajectory.AcceptFree),
            new JumpTrajectory.Requirement(new Vector2Int(1, 0), JumpTrajectory.AcceptStatic) });
        var c = new JumpTrajectory(new Vector2Int(1, 0), new[] { new JumpTrajectory.Requirement(new Vector2Int(1, 0), JumpTrajectory.AcceptNotStatic) });
        var automaton = JumpAutomaton.Build(new[] { a, b, c });

        var w = new AsciiWorld("  ", "*#");
        Assert.AreEqual(3UL, automaton.Run(ref w, w.Origin, 1));
        var w2 = new AsciiWorld("  ", "* ");
        Assert.AreEqual(4UL, automaton.Run(ref w2, w2.Origin, 1));
    }

    [Test]
    public void TooManyTrajectories_Throws()
    {
        var t = JumpTrajectory.Parse(MapGap1);
        Assert.Throws<ArgumentException>(() => JumpAutomaton.Build(Enumerable.Repeat(t, 65).ToArray()));
    }
}
