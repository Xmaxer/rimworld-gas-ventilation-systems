using System.Collections.Generic;
using System.IO;
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
                if (!(things[i] is Pawn p) || p.Faction != Faction.OfPlayer)
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

    public static Thing SpawnBuilding(ThingDef def, IntVec3 cell, Map map, Rot4 rot, ThingDef stuff = null)
    {
        Thing thing = ThingMaker.MakeThing(def, stuff ?? (def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null));
        if (def.CanHaveFaction)
        {
            thing.SetFactionDirect(Faction.OfPlayer);
        }
        return GenSpawn.Spawn(thing, cell, map, rot, WipeMode.Vanish);
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
        Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, forceGenerateNewPawn: true));
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
