using System.Collections.Generic;
using RimWorld;
using Verse;

namespace GasVentilation;

/// <summary>
/// Deconstructs gas pipes of all four networks, including hidden pipes under walls and vents, which the vanilla
/// deconstruct tool cannot reach because it targets the top building in a cell.
/// </summary>
public sealed class Designator_DeconstructGasPipes : Designator_Deconstruct
{
    public Designator_DeconstructGasPipes()
    {
        defaultLabel = "GV_DeconstructGasPipes".Translate();
        defaultDesc = "GV_DeconstructGasPipesDesc".Translate();
        icon = GasVentTextures.DeconstructPipes;
        hotKey = null;
    }

    public override AcceptanceReport CanDesignateCell(IntVec3 c)
    {
        if (!c.InBounds(Map) || c.Fogged(Map))
        {
            return false;
        }
        List<Thing> things = c.GetThingList(Map);
        for (int i = 0; i < things.Count; i++)
        {
            if (CanDesignateThing(things[i]).Accepted)
            {
                return true;
            }
        }
        return false;
    }

    public override void DesignateSingleCell(IntVec3 c)
    {
        List<Thing> things = c.GetThingList(Map);
        for (int i = things.Count - 1; i >= 0; i--)
        {
            if (CanDesignateThing(things[i]).Accepted)
            {
                DesignateThing(things[i]);
            }
        }
    }

    public override AcceptanceReport CanDesignateThing(Thing t)
    {
        ThingDef def = t.def.entityDefToBuild as ThingDef ?? t.def;
        if (!def.HasModExtension<GasPipeExtension>())
        {
            return false;
        }
        return base.CanDesignateThing(t);
    }
}
