using Assets.Scripts.Map;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Ai.Jump
{
    // Jedna trajektorie skoku doprava: pozadavky na bunky relativne k podlaze pod priserou.
    // Zadava se ASCII mapou, kazdy radek ohraniceny '|' z obou stran (mezery jsou vyznamne,
    // okraje je chrani pred orezanim v editoru). Horni radek = nejvyssi y. Znaky:
    //   '*'  podlaha pod priserou = pocatek [0,0], netestuje se
    //   ' '  musi byt volno
    //   '#'  musi byt doskociste - prave jedno, je to cil skoku
    //   'x'  nesmi byt doskociste (selze, kdyz by uspel kratsi skok nebo skok neni potreba)
    //   '?'  cokoliv (telo prisery)
    public sealed class JumpTrajectory
    {
        public readonly struct Requirement
        {
            public readonly Vector2Int Pos;
            public readonly byte Accept;   // bit (1 << JumpCellClass) = trida vyhovuje

            public Requirement(Vector2Int pos, byte accept)
            {
                Pos = pos;
                Accept = accept;
            }

            public bool Accepts(JumpCellClass c) => (Accept & (1 << (int)c)) != 0;
        }

        public const byte AcceptFree = 1 << (int)JumpCellClass.Free;
        public const byte AcceptStatic = 1 << (int)JumpCellClass.Static;
        public const byte AcceptNotStatic = (1 << (int)JumpCellClass.Free) | (1 << (int)JumpCellClass.Other);

        public readonly Vector2Int Target;   // podlaha doskoku
        public readonly Requirement[] Cells;

        public JumpTrajectory(Vector2Int target, Requirement[] cells)
        {
            Target = target;
            Cells = cells;
        }

        public static JumpTrajectory Parse(string map)
        {
            var rows = new List<string>();
            foreach (var rawLine in map.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                int start = line.IndexOf('|');
                if (start < 0)
                {
                    if (line.Trim().Length != 0)
                        throw new FormatException($"Radek trajektorie bez '|': \"{line}\"");
                    continue;
                }
                int end = line.LastIndexOf('|');
                if (end == start || line.Substring(end + 1).Trim().Length != 0)
                    throw new FormatException($"Radek trajektorie musi koncit '|': \"{line}\"");
                rows.Add(line.Substring(start + 1, end - start - 1));
            }
            if (rows.Count == 0)
                throw new FormatException("Prazdna trajektorie");

            Vector2Int? origin = null;
            Vector2Int? target = null;
            for (int r = 0; r < rows.Count; r++)
            {
                if (rows[r].Length != rows[0].Length)
                    throw new FormatException($"Radky trajektorie maji ruznou sirku:\n{map}");
                for (int c = 0; c < rows[r].Length; c++)
                {
                    char ch = rows[r][c];
                    if (ch == '*')
                        origin = origin == null ? new Vector2Int(c, r) : throw new FormatException($"Vic nez jedna '*':\n{map}");
                    else if (ch == '#')
                        target = target == null ? new Vector2Int(c, r) : throw new FormatException($"Vic nez jeden '#':\n{map}");
                }
            }
            if (origin == null)
                throw new FormatException($"Chybi '*':\n{map}");
            if (target == null)
                throw new FormatException($"Chybi '#':\n{map}");

            var o = origin.Value;
            var cells = new List<Requirement>();
            for (int r = 0; r < rows.Count; r++)
            {
                for (int c = 0; c < rows[r].Length; c++)
                {
                    byte accept = rows[r][c] switch
                    {
                        ' ' => AcceptFree,
                        '#' => AcceptStatic,
                        'x' => AcceptNotStatic,
                        '?' or '*' => (byte)0,
                        _ => throw new FormatException($"Neznamy znak '{rows[r][c]}':\n{map}"),
                    };
                    if (accept != 0)
                        cells.Add(new Requirement(ToRel(c, r, o), accept));
                }
            }

            return new JumpTrajectory(ToRel(target.Value.x, target.Value.y, o), cells.ToArray());
        }

        private static Vector2Int ToRel(int c, int r, Vector2Int origin) => new Vector2Int(c - origin.x, origin.y - r);
    }
}
