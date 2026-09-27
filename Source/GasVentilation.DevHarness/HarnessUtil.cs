using System.Collections.Generic;
using System.IO;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace GasVentilation.DevHarness;

public static class HarnessUtil
{
    public static string HarnessDir => Path.Combine(GenFilePaths.SaveDataFolderPath, "GasVentHarness");

    private static readonly string[] AllowedErrorFragments =
    {
        "Could not load Texture2D",
        "Failed to find any textures at",
        "Could not load UnityEngine.Texture2D",
        // Follows each missing Graphic_Single texture (vanilla builds a material from the null texture).
        "MatFrom with null sourceTex",
        // Environment noise: the dev machine subscribes to two Workshop copies of some unrelated mods. Logged by
        // ModLister while enumerating all installed mods, even inactive ones.
        "Tried loading mod with the same packageId multiple times"
    };

    public static CellRect ReserveArea(Map map, int index)
    {
        int x0 = 10 + (index % 4) * 30;
        int z0 = 10 + (index / 4) * 30;
        CellRect rect = new CellRect(x0, z0, 24, 24).ClipInsideMap(map);
        List<Thing> doomed = new List<Thing>();
        foreach (IntVec3 c in rect)
        {
            List<Thing> things = c.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                // Non-destroyable things (steam geysers, map-feature buildings) log an error when destroyed.
                if (things[i].def.destroyable && (!(things[i] is Pawn p) || p.Faction != Faction.OfPlayer))
                {
                    doomed.Add(things[i]);
                }
            }
            map.terrainGrid.SetTerrain(c, TerrainDefOf.Concrete);
            map.roofGrid.SetRoof(c, null);
            map.fogGrid.Unfog(c);
        }
        for (int i = 0; i < doomed.Count; i++)
        {
            if (!doomed[i].Destroyed)
            {
                doomed[i].Destroy(DestroyMode.Vanish);
            }
        }
        return rect;
    }

    /// <summary>
    /// Test rooms have no power grid, so RimWorld's own power-net logic recomputes any CompPowerTrader back
    /// to unpowered on its next update -- setting PowerOn once at spawn isn't enough (SensorScenario already
    /// had to work around this for its own sensor by re-forcing every tick). ReapplyForcedPower, called once
    /// per harness tick, keeps every power-requiring building any scenario has spawned forced on instead.
    /// </summary>
    private static readonly List<CompPowerTrader> ForcedPower = new List<CompPowerTrader>();

    public static Thing SpawnBuilding(ThingDef def, IntVec3 cell, Map map, Rot4 rot, ThingDef stuff = null)
    {
        Thing thing = ThingMaker.MakeThing(def, stuff ?? (def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null));
        if (def.CanHaveFaction)
        {
            thing.SetFactionDirect(Faction.OfPlayer);
        }
        Thing spawned = GenSpawn.Spawn(thing, cell, map, rot, WipeMode.Vanish);
        CompPowerTrader power = (spawned as ThingWithComps)?.GetComp<CompPowerTrader>();
        if (power != null)
        {
            power.PowerOn = true;
            ForcedPower.Add(power);
        }
        return spawned;
    }

    public static void ReapplyForcedPower()
    {
        for (int i = 0; i < ForcedPower.Count; i++)
        {
            CompPowerTrader power = ForcedPower[i];
            if (power?.parent?.Spawned == true)
            {
                power.PowerOn = true;
            }
        }
    }

    /// <summary>Builds walls on the border of <paramref name="outer"/>; optionally roofs the interior.</summary>
    public static CellRect BuildSealedRoom(Map map, CellRect outer, bool roofed, ThingDef wallStuff = null)
    {
        foreach (IntVec3 c in outer.EdgeCells)
        {
            SpawnBuilding(ThingDefOf.Wall, c, map, Rot4.North, wallStuff ?? ThingDefOf.Plasteel);
        }
        CellRect inner = outer.ContractedBy(1);
        if (roofed)
        {
            foreach (IntVec3 c in outer)
            {
                map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
            }
        }
        return inner;
    }

    public static Pawn SpawnPawn(PawnKindDef kind, Faction faction, IntVec3 cell, Map map)
    {
        PawnGenerationRequest request = new PawnGenerationRequest(kind, faction, forceGenerateNewPawn: true);
        if (ModsConfig.BiotechActive && kind.RaceProps.Humanlike)
        {
            // Random xenotypes (wasters, genes with toxic resistance) make gas scenarios non-deterministic.
            request.ForcedXenotype = XenotypeDefOf.Baseliner;
        }
        Pawn pawn = PawnGenerator.GeneratePawn(request);
        GenSpawn.Spawn(pawn, cell, map);
        return pawn;
    }

    public static void JumpCamera(IntVec3 cell)
    {
        Find.CameraDriver.JumpToCurrentMapLoc(cell);
    }

    public static void Screenshot(string name)
    {
        Directory.CreateDirectory(HarnessDir);
        // The dev log auto-opens on any logged error and would cover the map; the log file keeps the messages.
        EditWindow_Log log = Find.WindowStack.WindowOfType<EditWindow_Log>();
        if (log != null)
        {
            Find.WindowStack.TryRemove(log, doCloseSound: false);
        }
        ScreenCapture.CaptureScreenshot(Path.Combine(HarnessDir, name + ".png"));
    }

    public static void CloseBlockingWindows()
    {
        IList<Window> windows = Find.WindowStack.Windows;
        for (int i = windows.Count - 1; i >= 0; i--)
        {
            Window w = windows[i];
            if (w.forcePause || w.absorbInputAroundWindow)
            {
                Find.WindowStack.TryRemove(w, doCloseSound: false);
            }
        }
    }

    public static List<string> UnexpectedLoggedErrors()
    {
        List<string> result = new List<string>();
        using (Log.LockMessages())
        {
            foreach (LogMessage m in Log.Messages)
            {
                if (m.type != LogMessageType.Error)
                {
                    continue;
                }
                bool allowed = false;
                for (int i = 0; i < AllowedErrorFragments.Length; i++)
                {
                    if (m.text != null && m.text.Contains(AllowedErrorFragments[i]))
                    {
                        allowed = true;
                        break;
                    }
                }
                if (!allowed)
                {
                    result.Add(m.text);
                }
            }
        }
        return result;
    }

    public static string JsonEscape(string s)
    {
        if (s == null)
        {
            return "";
        }
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");
    }
}
