using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace GasVentilation;

/// <summary>Wall vents: the cell the vent blows into must be on the map and not blocked by a solid building.</summary>
public sealed class PlaceWorker_GasVentOutput : PlaceWorker
{
    private static readonly List<IntVec3> GhostCells = new List<IntVec3>(1);

    public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
    {
        IntVec3 output = OutputCellFor(checkingDef, loc, rot);
        if (!output.InBounds(map))
        {
            return "GV_VentOutputOutOfBounds".Translate();
        }
        Building edifice = output.GetEdifice(map);
        if (edifice != null && edifice != thingToIgnore && edifice.def.Fillage == FillCategory.Full && !(edifice is Building_Door))
        {
            return "GV_VentOutputBlocked".Translate();
        }
        return AcceptanceReport.WasAccepted;
    }

    public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
    {
        GhostCells.Clear();
        GhostCells.Add(OutputCellFor(def, center, rot));
        GenDraw.DrawFieldEdges(GhostCells, Color.cyan);
    }

    private static IntVec3 OutputCellFor(BuildableDef def, IntVec3 loc, Rot4 rot)
    {
        CompProperties_GasVent props = (def as ThingDef)?.GetCompProperties<CompProperties_GasVent>();
        IntVec3 offset = props?.outputOffset ?? IntVec3.Zero;
        return loc + offset.RotatedBy(rot);
    }
}
