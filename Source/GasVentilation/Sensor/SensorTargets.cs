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

    Default = Humanlikes | Mechanoids | Insectoids | Animals
}
