using System.Collections.Generic;
using RimWorld;
using Verse;

namespace GasVentilation;

/// <summary>
/// Thing class for every vent shape (wall/mounted/ceiling/floor). Adds a renamable custom label: purely
/// cosmetic, but useful now that ITab_GasSensor's trigger list no longer shows coordinates -- every vent gets
/// a unique auto-generated default name ("gas wall vent 1", "gas wall vent 2", ...), same pattern as
/// ZoneManager.NewZoneName, so rows stay distinguishable without it. The player can override it with the
/// standard vanilla rename dialog (the same base class Dialog_RenameGravship uses).
/// </summary>
public sealed class Building_GasVent : Building, IRenameable
{
    private string customLabel;

    public override string LabelNoCount => customLabel ?? base.LabelNoCount;

    public string RenamableLabel
    {
        get => customLabel ?? BaseLabel;
        set => customLabel = value;
    }

    public string BaseLabel => def.LabelCap;

    public string InspectLabel => RenamableLabel;

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);
        customLabel ??= GenerateUniqueName();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref customLabel, "gvCustomLabel");
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (Gizmo gizmo in base.GetGizmos())
        {
            yield return gizmo;
        }
        if (Faction != Faction.OfPlayer)
        {
            yield break;
        }
        yield return new Command_Action
        {
            defaultLabel = "Rename".Translate(),
            icon = TexButton.Rename,
            action = () => Find.WindowStack.Add(new Dialog_RenameGasVent(this))
        };
    }

    private string GenerateUniqueName()
    {
        GasDeviceRegistry devices = VentGasGrid.For(Map)?.Devices;
        for (int i = 1; i <= 1000; i++)
        {
            string candidate = def.label + " " + i;
            if (!NameTaken(candidate, devices))
            {
                return candidate;
            }
        }
        return def.label;
    }

    private bool NameTaken(string candidate, GasDeviceRegistry devices)
    {
        if (devices == null)
        {
            return false;
        }
        for (int i = 0; i < devices.Controllers.Count; i++)
        {
            Thing other = devices.Controllers[i].parent;
            if (other != this && other.LabelNoCount == candidate)
            {
                return true;
            }
        }
        return false;
    }
}
