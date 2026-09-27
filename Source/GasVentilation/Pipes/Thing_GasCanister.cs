using UnityEngine;
using Verse;

namespace GasVentilation;

/// <summary>
/// A filled gas canister (GV_Canister_{Gas}) that tracks how full it actually is, 0-1. Freshly crafted at the
/// compounder it defaults to 1 (fully full). Ejecting a manifold's fractional remainder on a gas switch, or on
/// deconstruction, creates one of these at less than 1 instead of discarding the remainder as an instant gas
/// burst -- a canister is an ordinary item (stored, hauled, sold) whatever its fill level.
///
/// Only stacks with another canister at the *same* fill (matching RimWorld's own HitPoints-style stacking
/// convention): freshly crafted full canisters (1.0) keep stacking together as before, and so do canisters at
/// any other fill that happens to match exactly, but two different partial fills never merge into one stack.
/// </summary>
public class Thing_GasCanister : ThingWithComps
{
    public float fill = 1f;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref fill, "gvFill", 1f);
    }

    public override bool CanStackWith(Thing other)
    {
        return base.CanStackWith(other) && other is Thing_GasCanister o && Mathf.Approximately(fill, o.fill);
    }

    public override Thing SplitOff(int count)
    {
        Thing split = base.SplitOff(count);
        if (split is Thing_GasCanister canister && split != this)
        {
            canister.fill = fill;
        }
        return split;
    }

    public override string GetInspectString()
    {
        string baseText = base.GetInspectString();
        if (fill >= 0.999f)
        {
            return baseText;
        }
        string fillLine = "GV_CanisterFill".Translate(Mathf.RoundToInt(fill * 100f));
        return baseText.NullOrEmpty() ? fillLine : baseText + "\n" + fillLine;
    }

    public override string LabelNoCount => fill >= 0.999f ? base.LabelNoCount : base.LabelNoCount + " (" + Mathf.RoundToInt(fill * 100f) + "%)";
}
