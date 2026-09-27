using RimWorld;
using UnityEngine;
using Verse;

namespace GasVentilation;

/// <summary>Replaces the old FloatMenu gas picker: a radio-button list of the four gases, plus the manifold's
/// current stored/pending state. Same role as ITab_Bills is to a work table -- a persistent panel instead of a
/// one-shot dropdown, since gas choice is a setting a player revisits, not a fire-and-forget action.</summary>
public sealed class ITab_GasManifold : ITab
{
    private static readonly Vector2 WinSize = new Vector2(260f, 200f);

    private CompGasManifold Manifold => (SelThing as ThingWithComps)?.GetComp<CompGasManifold>();

    public ITab_GasManifold()
    {
        size = WinSize;
        labelKey = "GV_GasManifoldTab";
    }

    protected override void FillTab()
    {
        CompGasManifold manifold = Manifold;
        if (manifold == null)
        {
            return;
        }
        Rect rect = new Rect(0f, 0f, WinSize.x, WinSize.y).ContractedBy(10f);
        Listing_Standard listing = new Listing_Standard();
        listing.Begin(rect);
        Text.Font = GameFont.Medium;
        listing.Label("GV_GasManifoldTab".Translate());
        Text.Font = GameFont.Small;
        listing.GapLine();

        GasDef target = manifold.PendingGas ?? manifold.ActiveGas;
        for (int i = 0; i < CompGasManifold.AllGases.Length; i++)
        {
            GasDef gas = CompGasManifold.AllGases[i];
            if (listing.RadioButton(gas.LabelCap, target == gas))
            {
                manifold.RequestGasChange(gas);
            }
        }

        listing.GapLine();
        string storedLine = manifold.ActiveGas == null
            ? "GV_ManifoldGasNone".Translate().ToString().CapitalizeFirst() + "."
            : "GV_ManifoldStored".Translate(manifold.ActiveGas.LabelCap, manifold.AmountStored.ToString("F1"), manifold.Props.storageCapacity.ToString("F0")).ToString();
        listing.Label(storedLine);
        if (manifold.PendingGas != null)
        {
            listing.Label("GV_ManifoldReconfiguring".Translate(manifold.PendingGas.LabelCap).ToString());
        }
        listing.End();
    }
}
