using PipeSystem;

namespace GasVentilation;

/// <summary>
/// Plain multi-network pipe segment. Up to four of these sit on one pipe Thing (one per gas), and VEF's stock
/// CompResource.CompInspectStringExtra prints a "stored in network" line per comp, plus (in dev mode) a raw
/// PipeNet.ToString() dump including Production/Consumption/Overflow fields our design never populates (we
/// draw directly via PipeNet.DrawAmongStorage, never registering as a VEF producer/consumer) -- always 0,
/// never useful. A pipe segment showing the whole network's totals, repeated on every tile and every gas, adds
/// nothing a player needs; that information belongs on the manifold (source) and vent (destination) instead.
/// </summary>
public sealed class CompGasPipe : CompResource
{
    public override string CompInspectStringExtra()
    {
        return null;
    }
}
