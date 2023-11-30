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
}


[Flags]
public enum VelocityFlags
{
    None,
    LimitVelocity = 1,
    DontAffectRb = 2,
    IsImpact = 4,
}
