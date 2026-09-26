using HarmonyLib;
using Verse;

namespace GasVentilation;

public sealed class GasVentilationMod : Mod
{
    public const string HarmonyId = "Xmaxer.GasVentilation";

    public static ModContentPack ContentPack { get; private set; }

    public GasVentilationMod(ModContentPack content) : base(content)
    {
        ContentPack = content;
        new Harmony(HarmonyId).PatchAll(typeof(GasVentilationMod).Assembly);
    }
}
