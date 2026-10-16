using Assets.Scripts.Ai.Jump;
using Assets.Scripts.Utils;
using System;
using UnityEngine;

namespace Assets.Scripts.Ai
{
    // Nastaveni AI druhu prisery: rule set + smysly + konfigurace stylu pohybu.
    // Pravidla jsou bezstavova, takze vsechny prisery, ktere maji tenhle asset, sdili tytez
    // instance pravidel. Poradi v poli = priorita (shora = nejvyssi).
    //
    // KONVENCE: ladici parametry stylu patri SEM (ne na komponentu stylu), seskupene pod [Header]
    // podle stylu. Pole stylu, ktery prisera nema, jsou mrtva a nic nestoji.
    [CreateAssetMenu]
    public class AiSettings : ScriptableObject
    {
        [Tooltip("Pravidla soupeřící o pohybový zámek. Pořadí = priorita (nahoře nejvyšší); první, jehož Test projde, zabere krok.")]
        [SerializeReference, TypePicker]
        public IAiRule[] Rules = Array.Empty<IAiRule>();

        [Tooltip("Reflexy - vyhodnocují se každý krok všechny, i když běží pravidlo; zámek neberou, mění jen kontext.")]
        [SerializeReference, TypePicker]
        public IAiModifier[] Modifiers = Array.Empty<IAiModifier>();

        [Header("Senses")]
        [Tooltip("Sdílený základ smyslů (archetyp, např. HerbivoreSenses).")]
        public SenseProfile SenseProfile;
        [Tooltip("Odchylky od profilu. Uplatní se jen nedefaultní pole záznamu (0/None/false = převzít z profilu; bool jde jen zapnout).")]
        public SenseConfig[] SenseOverrides = Array.Empty<SenseConfig>();

        [Header("Crawler")]
        [Tooltip("Nevleze do díry - hlídá hranu před sebou.")]
        public bool AvoidHoles = true;

        [Header("Jumper")]
        [Tooltip("Sada trajektorií skoku (JumpTrajectories) - podle velikosti a síly skoku příšery.")]
        public JumpSet JumpSet;

        [NonSerialized]
        private SenseConfig[] resolvedSenses;

        public SenseConfig[] ResolvedSenses => resolvedSenses ??= ResolveSenses();

        private SenseConfig[] ResolveSenses()
        {
            var resolved = new SenseConfig[SenseIds.Count];
            if (SenseProfile != null)
            {
                foreach (var c in SenseProfile.Senses)
                {
                    if (resolved[(int)c.Slot].Kind != SenseKind.None)
                        Debug.LogWarning($"{name}: SenseProfile {SenseProfile.name} ma vic zaznamu pro slot {c.Slot}");
                    resolved[(int)c.Slot] = c;
                }
            }

            foreach (var o in SenseOverrides)
            {
                ref var b = ref resolved[(int)o.Slot];
                b = new SenseConfig(in b, in o);
            }
            return resolved;
        }

        private void OnValidate() => resolvedSenses = null;
    }
}
