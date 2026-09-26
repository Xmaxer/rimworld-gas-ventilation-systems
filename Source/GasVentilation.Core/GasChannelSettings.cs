namespace GasVentilation.Core;

public struct GasChannelSettings
{
    /// <summary>Density removed per dissipation visit outdoors (fractions are applied deterministically).</summary>
    public float DissipationPerVisit;

    /// <summary>Multiplier on <see cref="DissipationPerVisit"/> for roofed cells.</summary>
    public float RoofedFactor;

    /// <summary>Extra density removed per visit per unit of vacuum (Odyssey).</summary>
    public float VacuumFactor;

    /// <summary>Minimum half-difference moved per diffusion step (vanilla uses 17).</summary>
    public int MinDiffusion;

    public bool Diffuses;

    /// <summary>Settings for a channel with no gas assigned: anything that appears there vanishes quickly.</summary>
    public static GasChannelSettings Unused => new GasChannelSettings
    {
        DissipationPerVisit = 255f,
        RoofedFactor = 1f,
        VacuumFactor = 0f,
        MinDiffusion = 17,
        Diffuses = false
    };
}
