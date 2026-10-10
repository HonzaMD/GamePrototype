using System;
using UnityEngine;

namespace Assets.Scripts.Ai
{
    // Sdileny archetyp smyslu (napr. "HerbivoreSenses"). Konkretni druh na nej odkazuje
    // z AiSettings a pripadne prepise jednotlive hodnoty pres SenseOverrides.
    [CreateAssetMenu]
    public class SenseProfile : ScriptableObject
    {
        [Tooltip("Smysly archetypu - nejvýš jeden záznam na slot.")]
        public SenseConfig[] Senses = Array.Empty<SenseConfig>();
    }
}
