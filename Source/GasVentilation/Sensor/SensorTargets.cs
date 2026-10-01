using System;

namespace GasVentilation;

[Flags]
public enum SensorTargets
{
    None = 0,
    Humanlikes = 1,
    Mechanoids = 2,
    Insectoids = 4,
    Animals = 8,

    /// <summary>Also trigger on non-hostile outsiders (visitors, traders, wild animals).</summary>
    IncludeNonHostile = 16,

    /// <summary>Also trigger on the player's own colonists. Off by default: this is what lets a sensor lock a
    /// door against its own colony, not just intruders, so it needs an explicit opt-in.</summary>
    Colonists = 32,

    Default = Humanlikes | Mechanoids | Insectoids | Animals
}
