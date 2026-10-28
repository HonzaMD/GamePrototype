using System;
using UnityEngine;

namespace Assets.Scripts.Ai
{
    public enum BounceStyle
    {
        Exact,      // odraz podle normaly
        Axis,       // odraz podle normaly zaokrouhlene na osu, heading pak srovna na FixedAngle
        Random,     // odraz podle normaly +- BounceSpread
    }

    // Clen krivky letu: vektor delky Amplitude, ktery se otaci vuci headingu s periodou Period.
    [Serializable]
    public struct FlightTerm
    {
        [Tooltip("Délka vektoru vůči headingu (ten má 1). Nad 1 dělá smyčky.")]
        public float Amplitude;
        [Tooltip("Perioda otočky [s], znaménko = směr otáčení. 0 = vektor stojí (trvalá odchylka).")]
        public float Period;
        [Tooltip("Počáteční fáze [°].")]
        public float Phase;
        [Tooltip("Jen Roam: místo přičtení vektoru stáčí přímo heading - velké oblouky, s Chaos > 1 bloudění. Amplitude se ignoruje.")]
        public bool RotateHeading;
    }

    // Krivka letu - epicykly natocene podle headingu:
    //   u = h + sum A_i * rot(h, phi_i),   v = V * u / (1 + sum A_i)
    // Polomer smycky jednoho clenu r = V*A / ((1+A)*omega) - se zpomalenim se smycky zmensuji.
    // Chaos moduluje uhlovou rychlost i amplitudu kazdeho clenu Perlinovym sumem: x * (1 + Chaos * N(t)), N v <-1, 1>.
    [Serializable]
    public class FlightPattern
    {
        public FlightTerm[] Terms = Array.Empty<FlightTerm>();
        [Tooltip("Náhodnost, 0 = přesná křivka. Nad 1 se může otočit i směr rotace.")]
        public float Chaos;
        [Tooltip("Časové měřítko šumu [s] - jak rychle se náhodnost mění.")]
        public float NoiseTime = 1;
    }
}
