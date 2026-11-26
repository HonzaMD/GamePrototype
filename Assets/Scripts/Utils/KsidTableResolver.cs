using Assets.Scripts.Core;
using System.Collections.Generic;

namespace Assets.Scripts.Utils
{
    // Klic bunky pro KsidTableResolver. Implementuj na structu - generika pak vola bez boxingu.
    public interface IKsidPair
    {
        Ksid Row { get; }
        Ksid Col { get; }
    }

    // Klic bez hodnoty (editor, testy).
    public readonly struct KsidPair : IKsidPair
    {
        public Ksid Row { get; }
        public Ksid Col { get; }

        public KsidPair(Ksid row, Ksid col)
        {
            Row = row;
            Col = col;
        }

        public static implicit operator KsidPair((Ksid Row, Ksid Col) t) => new(t.Row, t.Col);
    }

    // Hledani bunky v KsidTable, obe osy najednou (Docs/ksid-table.md):
    // kandidati = vyplnene bunky, jejichz radek je predek-nebo-rovno row a sloupec predek-nebo-rovno col.
    // Kandidat vypada, kdyz jiny kandidat je specifictejsi-nebo-stejny na obou osach.
    // Zbyde 0 = nic, 1 = vysledek, vic = konflikt.
    public static class KsidTableResolver
    {
        // winners: indexy do filled. Vraci jejich pocet.
        public static int Resolve<T>(Ksids ksids, IReadOnlyList<T> filled, Ksid row, Ksid col, List<int> winners)
            where T : IKsidPair
        {
            winners.Clear();
            for (int i = 0; i < filled.Count; i++)
            {
                var f = filled[i];
                if (ksids.IsParentOrEqual(row, f.Row) && ksids.IsParentOrEqual(col, f.Col))
                    winners.Add(i);
            }

            // Mazani za behu je v poradku: prebijeni je tranzitivni, minimalni kandidati nevypadnou nikdy.
            for (int a = winners.Count - 1; a >= 0; a--)
            {
                var x = filled[winners[a]];
                for (int b = 0; b < winners.Count; b++)
                {
                    var y = filled[winners[b]];
                    if (b != a && (y.Row != x.Row || y.Col != x.Col) && ksids.IsParentOrEqual(y.Row, x.Row) && ksids.IsParentOrEqual(y.Col, x.Col))
                    {
                        winners.RemoveAt(a);
                        break;
                    }
                }
            }

            return winners.Count;
        }
    }
}
