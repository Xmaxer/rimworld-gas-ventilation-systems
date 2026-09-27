using PipeSystem;
using UnityEngine;
using Verse;

namespace GasVentilation;

/// <summary>
/// One of up to four per-gas resource traders on a vent building (one per gas network). It never ticks itself:
/// <see cref="GasDeviceRegistry"/> pulses it every 25 ticks while it is emitting. Each pulse draws exactly what
/// the grid accepted, so vents start instantly, stall under back-pressure, and stop when the manifolds are empty
/// -- capped a second way by <see cref="LastShare"/>, this pulse's proportional cut of the network's throughput
/// (see <see cref="GasDeviceRegistry"/>'s slot accounting).
/// </summary>
public sealed class CompGasVent : CompResource
{
    public new CompProperties_GasVent Props => (CompProperties_GasVent)props;

    public GasDef Gas => Props.gas;

    private CompGasVentController controller;

    public CompGasVentController Controller => controller ??= parent.GetComp<CompGasVentController>();

    public IntVec3 OutputCell => Controller.OutputCell;

    /// <summary>This pulse's share (0-1) of the network's per-manifold-slot throughput budget.</summary>
    public float LastShare { get; internal set; } = 1f;

    public string LastSharePercent => Mathf.RoundToInt(LastShare * 100f) + "%";

    public bool Emitting => Controller.Emitting && (Controller.Selection & Gas.ventFlag) != 0;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        VentGasGrid.For(parent.Map)?.Devices.Register(this);
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        base.PostDeSpawn(map, mode);
        VentGasGrid.For(map)?.Devices.Deregister(this);
    }

    /// <summary>
    /// VEF's stock CompResource.CompInspectStringExtra would print a "stored in network" line per comp (up to
    /// four times over on this one vent) plus, in dev mode, a raw PipeNet.ToString() dump of
    /// production/consumption/overflow figures our design never populates. The controller already shows the
    /// one line that actually matters here: this gas's live output share.
    /// </summary>
    public override string CompInspectStringExtra()
    {
        return null;
    }

    public void Pulse()
    {
        PipeNet net = PipeNet;
        if (net == null)
        {
            return;
        }
        float stored = net.CurrentStored();
        if (stored <= 0f)
        {
            return;
        }
        GasDef gas = Gas;
        int budget = Mathf.Min(gas.emitPerPulse, Mathf.FloorToInt(stored * gas.densityPerCanister));
        budget = Mathf.FloorToInt(budget * LastShare);
        if (budget <= 0)
        {
            return;
        }
        int accepted = VentGasGrid.For(parent.Map).EmitAround(OutputCell, gas, budget);
        if (accepted > 0)
        {
            net.DrawAmongStorage(accepted / gas.densityPerCanister, out _, null);
        }
    }
}
