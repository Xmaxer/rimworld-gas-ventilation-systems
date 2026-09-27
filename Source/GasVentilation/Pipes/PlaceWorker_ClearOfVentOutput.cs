using System.Collections.Generic;
using RimWorld;
using Verse;

namespace GasVentilation;

/// <summary>
/// Pipes: refuse placement in a cell any spawned vent is currently blowing gas into. Nothing stops a pipe
/// sitting there mechanically (pipes are non-edifice and never block gas), but it looks wrong -- the vent's
/// output cell is where gas disperses into the room, not a connection point.
/// </summary>
public sealed class PlaceWorker_ClearOfVentOutput : PlaceWorker
{
    public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
    {
        IReadOnlyList<CompGasVent> vents = VentGasGrid.For(map).Devices.Vents;
        for (int i = 0; i < vents.Count; i++)
        {
            CompGasVent vent = vents[i];
            if (vent.parent != thingToIgnore && vent.OutputCell == loc)
            {
                return "GV_PipeBlocksVentOutput".Translate();
            }
        }
        return AcceptanceReport.WasAccepted;
    }
}
