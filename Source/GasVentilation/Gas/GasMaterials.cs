using GasVentilation.Core;
using UnityEngine;
using Verse;

namespace GasVentilation;

/// <summary>
/// One material per gas and density band. Reuses vanilla's cloud texture with the static TransparentPostLight
/// shader, tinted per gas, so no shader or texture has to ship. Band opacity rises with density.
/// (Vanilla's GasRotating shader was tried first and drew nothing visible with a tinted material; see
/// docs/implementation-notes.md, M2 Task 6.)
/// </summary>
[StaticConstructorOnStartup]
public static class GasMaterials
{
    public const int RenderQueue = 3000;
    private const string CloudTexPath = "Things/Gas/GasCloudThickA";
    private static readonly float[] BandAlpha = { 0f, 0.35f, 0.65f, 1f };
    private static readonly Material[,] Materials = new Material[GasPacking.Channels, 4];

    static GasMaterials()
    {
        for (int channel = 0; channel < GasPacking.Channels; channel++)
        {
            GasDef def = GasDefRegistry.ForChannel(channel);
            if (def == null)
            {
                continue;
            }
            for (int band = GasBands.Low; band <= GasBands.High; band++)
            {
                Color color = def.color;
                color.a = BandAlpha[band];
                Materials[channel, band] = MaterialPool.MatFrom(CloudTexPath, ShaderDatabase.TransparentPostLight, color, RenderQueue);
            }
        }
    }

    public static Material Get(int channel, int band)
    {
        return Materials[channel, band];
    }
}
