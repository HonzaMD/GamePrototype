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
    public struct KsidTableEntry<TCell> 
        where TCell : struct
    {
        public Ksid Row;
        public Ksid Col;
        public TCell Value;
    }

    // TCell: maly struct s fieldy float/int/bool. Chybejici zaznam = null bunka (dedi).
    public abstract class KsidTable<TCell> : KsidTable 
        where TCell : struct
    {
        [SerializeField]
        private List<KsidTableEntry<TCell>> entries = new();

        public IReadOnlyList<KsidTableEntry<TCell>> Entries => entries;
        public override Type CellType => typeof(TCell);
    }
}
