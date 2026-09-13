using System;
using UnityEngine;

namespace Assets.Scripts.Ai
{
    // Rule set druhu prisery. Pravidla jsou bezstavova, takze vsechny prisery, ktere maji tenhle
    // asset, sdili tytez instance pravidel - lze ho tedy sdilet i napric telami (styly pohybu).
    // Poradi v poli = priorita (shora = nejvyssi).
    [CreateAssetMenu]
    public class AiSettings : ScriptableObject
    {
        [SerializeReference]
        public IAiRule[] Rules = Array.Empty<IAiRule>();

        [SerializeReference]
        public IAiModifier[] Modifiers = Array.Empty<IAiModifier>();
    }
}
