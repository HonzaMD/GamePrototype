using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class PhysicsConsts
{
    //public const float SpToRbLimitsMultiplier = 12;
    public const float SpToRbLimitsMultiplier = 12;
    public const float ImpactDump = 0.2f;
    public const float ImpulseToMass = 0.5f / (9 * 0.02f); // davam tam polovinu sve klidove vahy
    public const float ImpulseToMassDumped = ImpulseToMass * ImpactDump;
    public const float SandCombinerTransferMinimum = 0.3f;
    public const float ImpactVelocitySqr = 0.5f * 0.5f;
    public const float TooSmallSqr = 0.1f * 0.1f;

    // Impact damage: dmg = (impactSpeed - threshold) * scale * massFactor
    public const float ImpactDmgThreshold = 5f * 5f; // m/s
    public const float ImpactDmgThresholdSpring = 8.5f * 8.5f; // m/s
    public const float ImpactDmgScale = 0.8f;

    // Knife damage: dmg = baseDmg * max(minFactor, v / refSpeed)
    public const float KnifeDmgRefSpeed = 5f; // m/s, typicka rychlost bodnutí
    public const float KnifeDmgMinFactor = 0.3f;
}


[Flags]
public enum VelocityFlags
{
    None,
    LimitVelocity = 1,
    DontAffectRb = 2,
    IsImpact = 4,
}
