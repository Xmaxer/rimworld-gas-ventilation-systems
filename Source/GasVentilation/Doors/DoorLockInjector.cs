using System.Linq;
using RimWorld;
using Verse;

namespace GasVentilation;

/// <summary>
/// Makes every powered door on the map lockable -- not just vanilla's Autodoor, but any mod's powered door too
/// -- without an XML patch per def (which would have to be maintained against every mod that might add one).
/// Runs once, after all defs are loaded but before any map exists, and adds CompProperties_DoorLock directly to
/// each matching ThingDef's comp list. A comp added this way is indistinguishable from one declared in the def's
/// own XML: every instance (including ones already placed in an existing save) gets it from the moment the def
/// is used, no migration needed.
/// </summary>
[StaticConstructorOnStartup]
internal static class DoorLockInjector
{
    static DoorLockInjector()
    {
        int count = 0;
        foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (!typeof(Building_Door).IsAssignableFrom(def.thingClass))
            {
                continue;
            }
            if (def.comps == null || !def.comps.Any(c => c is CompProperties_Power))
            {
                continue;
            }
            if (def.comps.Any(c => c is CompProperties_DoorLock))
            {
                continue;
            }
            def.comps.Add(new CompProperties_DoorLock());
            count++;
        }
        if (count > 0)
        {
            Log.Message($"[GasVentilation] Made {count} powered door def(s) lockable by intruder sensors.");
        }
    }
}
