using Multiplayer.API;
using Verse;

namespace GasVentilation;

[StaticConstructorOnStartup]
internal static class MultiplayerInit
{
    static MultiplayerInit()
    {
        if (MP.enabled)
        {
            MP.RegisterAll();
        }
    }
}
