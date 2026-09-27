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
        IReadOnlyList<CompGasVentController> controllers = VentGasGrid.For(map).Devices.Controllers;
        for (int i = 0; i < controllers.Count; i++)
        {
            CompGasVentController controller = controllers[i];
            if (controller.parent != thingToIgnore && controller.OutputCell == loc)
            {
                return "GV_PipeBlocksVentOutput".Translate();
            }
        }
        return AcceptanceReport.WasAccepted;
    }
}
