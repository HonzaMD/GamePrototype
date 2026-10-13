using Assets.Scripts.Map;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Ai.Jump
{
    public interface IJumpCellSource
    {
        JumpCellClass Get(Vector2Int cell);
    }

    // Automat, ktery jednim pruchodem otestuje vsechny trajektorie (max 64) najednou. Kazdy stav testuje
    // jednu bunku a podle jeji tridy skoci na dalsi stav. Vysledek je maska trajektorii, kterymi lze
    // skocit. Bunky se testuji od nejblizsich, kazda nejvys jednou; bunky, na kterych uz nezavisi zadna
    // ziva trajektorie, se preskakuji.
    //
    // Trajektorie, ktera projde svou posledni bunku, se hned prida do vysledku (Emit na prechodu) a ze stavu
    // zmizi. Stav tak nese jen nerozhodnute trajektorie - jinak by se stavy nasobily kombinacemi uz hotovych
    // vysledku (realna sada 28 trajektorii: 61k stavu misto 2k).
    //
    // Za behu se cte jen states[]: relativni bunka i to, co prechod prida do vysledku, je primo ve stavu.
    public sealed class JumpAutomaton
    {
        public const int MaxStates = short.MaxValue;   // indexy musi projit do short
        private const short End = -1;
        private const int SingleEmits = 64;
        private const int MaxEmits = byte.MaxValue - SingleEmits;

        // Emit* = co prechod prida do vysledku: 0 = nic, 1..64 = trajektorie e - 1,
        // 65..255 = emits[e - 65] (vic trajektorii najednou, vzacne).
        public struct State
        {
            public sbyte Dx, Dy;
            public byte EmitFree, EmitStatic, EmitOther;
            public short NextFree, NextStatic, NextOther;
        }

        private readonly State[] states;
        private readonly ulong[] emits;

        public int StateCount => states.Length;

        private JumpAutomaton(State[] states, ulong[] emits)
        {
            this.states = states;
            this.emits = emits;
        }

        // origin = podlaha pod priserou, dir = +1 doprava, -1 doleva (zrcadleni).
        public ulong Run<T>(ref T source, Vector2Int origin, int dir)
            where T : struct, IJumpCellSource
        {
            ulong result = 0;
            int s = states.Length > 0 ? 0 : End;
            while (s != End)
            {
                ref var st = ref states[s];
                var c = source.Get(new Vector2Int(origin.x + st.Dx * dir, origin.y + st.Dy));
                int e;
                if (c == JumpCellClass.Free)
                {
                    e = st.EmitFree;
                    s = st.NextFree;
                }
                else if (c == JumpCellClass.Static)
                {
                    e = st.EmitStatic;
                    s = st.NextStatic;
                }
                else
                {
                    e = st.EmitOther;
                    s = st.NextOther;
                }
                if (e != 0)
                    result |= e <= SingleEmits ? 1UL << (e - 1) : emits[e - SingleEmits - 1];
            }
            return result;
        }

        // Stav = (k, alive): k = index dalsi bunky v globalnim poradi, alive = zive nerozhodnute trajektorie.
        // Hodnoty otestovanych bunek si pamatovat netreba - pozadavky trajektorie jsou konjunkce, takze
        // staci vedet, kdo zije. Stejne (k, alive) se slucuji, vznikne DAG.
        public static JumpAutomaton Build(IReadOnlyList<JumpTrajectory> trajectories)
        {
            if (trajectories.Count > 64)
                throw new ArgumentException($"JumpAutomaton: max 64 trajektorii, zadano {trajectories.Count}");

            var cellSet = new HashSet<Vector2Int>();
            foreach (var t in trajectories)
                foreach (var r in t.Cells)
                    cellSet.Add(r.Pos);

            var cells = new List<Vector2Int>(cellSet);
            cells.Sort((a, b) =>
            {
                int d = a.sqrMagnitude.CompareTo(b.sqrMagnitude);
                if (d == 0)
                    d = a.y.CompareTo(b.y);
                if (d == 0)
                    d = a.x.CompareTo(b.x);
                return d;
            });
            foreach (var c in cells)
            {
                if (c.x < sbyte.MinValue || c.x > sbyte.MaxValue || c.y < sbyte.MinValue || c.y > sbyte.MaxValue)
                    throw new ArgumentException($"JumpAutomaton: bunka {c} mimo rozsah sbyte");
            }

            var cellIndex = new Dictionary<Vector2Int, int>();
            for (int k = 0; k < cells.Count; k++)
                cellIndex.Add(cells[k], k);

            int K = cells.Count;
            var relevant = new ulong[K];
            var pass = new ulong[K * 3];
            ulong all = 0;
            for (int i = 0; i < trajectories.Count; i++)
            {
                ulong bit = 1UL << i;
                all |= bit;
                foreach (var r in trajectories[i].Cells)
                {
                    int k = cellIndex[r.Pos];
                    if ((relevant[k] & bit) != 0)
                        throw new ArgumentException($"JumpAutomaton: trajektorie {i} ma bunku {r.Pos} dvakrat");
                    relevant[k] |= bit;
                    for (int c = 0; c < 3; c++)
                    {
                        if (r.Accepts((JumpCellClass)c))
                            pass[k * 3 + c] |= bit;
                    }
                }
            }
            for (int k = 0; k < K; k++)
            {
                // Bunka, kterou trajektorie netestuje, ji nezabije.
                for (int c = 0; c < 3; c++)
                    pass[k * 3 + c] |= ~relevant[k];
            }

            var builder = new Builder(cells, relevant, pass);
            builder.Run(all);
            return builder.Finish();
        }

        private sealed class Builder
        {
            private readonly List<Vector2Int> cells;
            private readonly ulong[] relevant;
            private readonly ulong[] pass;
            private readonly ulong[] rest;   // [k] = trajektorie s nejakou bunkou >= k

            private readonly Dictionary<(int k, ulong alive), short> stateIds = new();
            private readonly List<(int k, ulong alive)> stateKeys = new();
            private readonly List<State> states = new();
            private readonly Dictionary<ulong, byte> emitIds = new();
            private readonly List<ulong> emits = new();

            public Builder(List<Vector2Int> cells, ulong[] relevant, ulong[] pass)
            {
                this.cells = cells;
                this.relevant = relevant;
                this.pass = pass;
                rest = new ulong[cells.Count + 1];
                for (int k = cells.Count - 1; k >= 0; k--)
                    rest[k] = rest[k + 1] | relevant[k];
            }

            public void Run(ulong all)
            {
                Normalize(0, all);   // koren = stav 0, pokud je co testovat
                for (int s = 0; s < stateKeys.Count; s++)
                {
                    var (k, alive) = stateKeys[s];
                    ulong finish = alive & relevant[k] & ~rest[k + 1];   // zive trajektorie, pro ktere je bunka posledni
                    ulong passFree = alive & pass[k * 3 + (int)JumpCellClass.Free];
                    ulong passStatic = alive & pass[k * 3 + (int)JumpCellClass.Static];
                    ulong passOther = alive & pass[k * 3 + (int)JumpCellClass.Other];
                    states.Add(new State
                    {
                        Dx = (sbyte)cells[k].x,
                        Dy = (sbyte)cells[k].y,
                        EmitFree = Emit(finish & passFree),
                        EmitStatic = Emit(finish & passStatic),
                        EmitOther = Emit(finish & passOther),
                        NextFree = Normalize(k + 1, passFree),
                        NextStatic = Normalize(k + 1, passStatic),
                        NextOther = Normalize(k + 1, passOther),
                    });
                }
            }

            private short Normalize(int k, ulong alive)
            {
                alive &= rest[k];
                while (alive != 0 && (relevant[k] & alive) == 0)
                    k++;
                if (alive == 0)
                    return End;

                if (!stateIds.TryGetValue((k, alive), out var s))
                {
                    if (stateKeys.Count >= MaxStates)
                        throw new InvalidOperationException($"JumpAutomaton: exploze stavu, vic nez {MaxStates}");
                    s = (short)stateKeys.Count;
                    stateKeys.Add((k, alive));
                    stateIds.Add((k, alive), s);
                }
                return s;
            }

            private byte Emit(ulong mask)
            {
                if (mask == 0)
                    return 0;
                if ((mask & (mask - 1)) == 0)
                {
                    int bit = 0;
                    while ((mask >> bit) != 1)
                        bit++;
                    return (byte)(bit + 1);
                }
                if (!emitIds.TryGetValue(mask, out var id))
                {
                    if (emits.Count >= MaxEmits)
                        throw new InvalidOperationException($"JumpAutomaton: vic nez {MaxEmits} ruznych vysledku prechodu");
                    id = (byte)(SingleEmits + 1 + emits.Count);
                    emits.Add(mask);
                    emitIds.Add(mask, id);
                }
                return id;
            }

            public JumpAutomaton Finish() => new JumpAutomaton(states.ToArray(), emits.ToArray());
        }
    }
}
