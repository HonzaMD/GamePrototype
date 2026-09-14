using Assets.Scripts.Utils;
using System;
using UnityEngine;

namespace Assets.Scripts.Ai
{
    // Nastaveni AI druhu prisery: rule set + konfigurace stylu pohybu.
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

        [Header("Crawler")]
        public bool AvoidHoles = true;   // nevleze do diry - hlida hranu pred sebou
    }
}
