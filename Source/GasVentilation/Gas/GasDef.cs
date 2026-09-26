using System.Collections.Generic;
using GasVentilation.Core;
using UnityEngine;
using Verse;

namespace GasVentilation;

public enum GasTargetRule
{
    OrganicBreathers,
    Machines,
    Insectoids
}

public enum GasProtectionKind
{
    None,
    Respiratory,
    Electromagnetic
}

/// <summary>One of the (at most four) gases simulated by <see cref="VentGasGrid"/>.</summary>
public sealed class GasDef : Def
{
    /// <summary>Byte channel 0-3 in the packed grid. Must be unique.</summary>
    public int channel = -1;

    public Color color = Color.white;

    // Simulation
    public float dissipationPerVisit = 8f;
    public float roofedFactor = 0.75f;
    public float vacuumFactor = 25f;
    public int minDiffusion = 17;
    public bool diffuses = true;

    // Pipes and vents
    /// <summary>Grid density units released by one canister.</summary>
    public float densityPerCanister = 12495f;

    /// <summary>Density units a vent tries to emit per pulse (every 25 ticks).</summary>
    public int emitPerPulse = 240;

    /// <summary>Hidden pipe of this gas's network (used to restore a pipe when a ceiling vent falls).</summary>
    public ThingDef hiddenPipe;

    // Effects
    public GasTargetRule targets;
    public GasProtectionKind protection;
    public HediffDef exposureHediff;

    /// <summary>Exposure-hediff severity gained per tick at density 255 with no protection.</summary>
    public float dosePerTickAtFullDensity = 0.0005f;

    public bool scaleDoseByBodySize;

    public GasChannelSettings ToChannelSettings()
    {
        return new GasChannelSettings
        {
            DissipationPerVisit = dissipationPerVisit,
            RoofedFactor = roofedFactor,
            VacuumFactor = vacuumFactor,
            MinDiffusion = minDiffusion,
            Diffuses = diffuses
        };
    }

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors())
        {
            yield return error;
        }
        if (channel < 0 || channel >= GasPacking.Channels)
        {
            yield return $"channel must be between 0 and {GasPacking.Channels - 1}";
        }
        if (densityPerCanister <= 0f)
        {
            yield return "densityPerCanister must be positive";
        }
        if (emitPerPulse <= 0)
        {
            yield return "emitPerPulse must be positive";
        }
        if (minDiffusion < 1)
        {
            yield return "minDiffusion must be at least 1";
        }
        if (exposureHediff == null)
        {
            yield return "exposureHediff is required";
        }
    }
}
