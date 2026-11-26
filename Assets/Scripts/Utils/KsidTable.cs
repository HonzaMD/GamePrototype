using System;
using System.Collections.Generic;
using Assets.Scripts.Core;
using UnityEngine;

namespace Assets.Scripts.Utils
{
    // Globalni tabulka hodnot indexovana dvojici Ksid (radek x sloupec), s dedenim podle hierarchie Ksid.
    // Popis a pravidla dedeni: Docs/ksid-table.md. Edituje se v KsidTableWindow.
    public abstract class KsidTable : ScriptableObject
    {
        public const string RowsField = nameof(Rows);
        public const string ColumnsField = nameof(Columns);
        public const string EntriesField = "entries";

        [Tooltip("Ksid radku v poradi zobrazeni")]
        public List<Ksid> Rows = new();
        [Tooltip("Ksid sloupcu v poradi zobrazeni")]
        public List<Ksid> Columns = new();

        public abstract Type CellType { get; }
        public virtual string RowsLabel => "Řádky";
        public virtual string ColumnsLabel => "Sloupce";
    }

    [Serializable]
    public struct KsidTableEntry<TCell> : IKsidPair
        where TCell : struct
    {
        public Ksid Row;
        public Ksid Col;
        public TCell Value;

        readonly Ksid IKsidPair.Row => Row;
        readonly Ksid IKsidPair.Col => Col;
    }

    // TCell: maly struct s fieldy float/int/bool. Chybejici zaznam = null bunka (dedi).
    public abstract class KsidTable<TCell> : KsidTable 
        where TCell : struct
    {
        private const int conflict = -2;
        private const int empty = -1;

        [SerializeField]
        private List<KsidTableEntry<TCell>> entries = new();

        private readonly Dictionary<(Ksid, Ksid), int> indexCache = new();

        public IReadOnlyList<KsidTableEntry<TCell>> Entries => entries;
        public override Type CellType => typeof(TCell);

        // Cache drzi indexy do entries - po jakekoli zmene dat neplatna (inspector, KsidTableWindow, Undo).
        private void OnValidate() => indexCache.Clear();

        public TCell Get(Ksid row, Ksid col)
        {
            if (!TryGet(row, col, out var ret))
                throw new InvalidOperationException($"Bunka ({row}, {col}) nenalezena");
            return ret;
        }

        public bool TryGet(Ksid row, Ksid col, out TCell value)
        {
            if (!indexCache.TryGetValue((row, col), out var index))
            {
                var winners = ListPool<int>.Rent();
                KsidTableResolver.Resolve(Game.Instance.Ksids, Entries, row, col, winners);

                index = winners.Count == 0 ? empty : winners.Count > 1 ? conflict : winners[0];
                indexCache.Add((row, col), index);
                winners.Return();
            }

            if (index >= 0)
            {
                value = entries[index].Value;
                return true;
            }
            else if (index == conflict)
            {
                throw new InvalidOperationException($"Bunka ({row}, {col}) neni definovana a ma vice parentu v konfliktu");
            }
            else
            {
                value = default;
                return false;
            }
        }
    }
}
