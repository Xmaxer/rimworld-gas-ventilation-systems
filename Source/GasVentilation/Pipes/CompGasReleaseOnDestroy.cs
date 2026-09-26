using UnityEngine;
using Verse;

namespace GasVentilation;

/// <summary>Filled canisters destroyed by damage (fire, explosions) release their gas where they were.</summary>
public sealed class CompGasReleaseOnDestroy : ThingComp
{
    public CompProperties_GasReleaseOnDestroy Props => (CompProperties_GasReleaseOnDestroy)props;

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        base.PostDestroy(mode, previousMap);
        if (previousMap == null || (mode != DestroyMode.KillFinalize && mode != DestroyMode.KillFinalizeLeavingsOnly))
        {
            return;
        }
        int amount = Mathf.RoundToInt(Props.gas.densityPerCanister * Props.canistersPerItem * parent.stackCount);
        VentGasGrid.For(previousMap)?.ReleaseBurst(parent.Position, Props.gas, amount);
    }
}
