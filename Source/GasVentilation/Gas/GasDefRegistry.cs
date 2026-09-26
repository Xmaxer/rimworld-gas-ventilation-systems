using System.Collections.Generic;
using GasVentilation.Core;
using Verse;

namespace GasVentilation;

/// <summary>GasDefs indexed by channel, built once after defs load.</summary>
[StaticConstructorOnStartup]
public static class GasDefRegistry
{
    private static readonly GasDef[] ByChannel = new GasDef[GasPacking.Channels];

    static GasDefRegistry()
    {
        List<GasDef> defs = DefDatabase<GasDef>.AllDefsListForReading;
        for (int i = 0; i < defs.Count; i++)
        {
            GasDef def = defs[i];
            if (def.channel < 0 || def.channel >= GasPacking.Channels)
            {
                continue;
            }
            if (ByChannel[def.channel] != null)
            {
                Log.Error($"[GasVentilation] GasDefs {ByChannel[def.channel].defName} and {def.defName} both use channel {def.channel}; ignoring {def.defName}.");
                continue;
            }
            ByChannel[def.channel] = def;
        }
    }

    public static GasDef ForChannel(int channel)
    {
        return ByChannel[channel];
    }

    public static GasChannelSettings[] BuildChannelSettings()
    {
        GasChannelSettings[] result = new GasChannelSettings[GasPacking.Channels];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = ByChannel[i]?.ToChannelSettings() ?? GasChannelSettings.Unused;
        }
        return result;
    }
}
