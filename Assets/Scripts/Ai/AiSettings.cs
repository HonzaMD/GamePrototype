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
        [SerializeReference, TypePicker]
        public IAiRule[] Rules = Array.Empty<IAiRule>();

        [SerializeReference, TypePicker]
        public IAiModifier[] Modifiers = Array.Empty<IAiModifier>();

        [Header("Senses")]
        public SenseProfile SenseProfile;                              // sdileny zaklad
        public SenseConfig[] SenseOverrides = Array.Empty<SenseConfig>(); // jen odchylky, per-field

        [Header("Crawler")]
        public bool AvoidHoles = true;   // nevleze do diry - hlida hranu pred sebou

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
                b.Slot = o.Slot;
                if (o.Kind != SenseKind.None) b.Kind = o.Kind;
                if (o.Ksid != default) b.Ksid = o.Ksid;
                if (o.Radius != 0) b.Radius = o.Radius;
                if (o.NeedsSight) b.NeedsSight = true;
                if (o.SkipUnreachable) b.SkipUnreachable = true;
                if (o.TrackRadius != 0) b.TrackRadius = o.TrackRadius;
            }
            return resolved;
        }

        private void OnValidate() => resolvedSenses = null;
    }
}
