using System.Collections.Generic;
using Multiplayer.API;
using PipeSystem;
using RimWorld;
using UnityEngine;
using Verse;

namespace GasVentilation;

/// <summary>
/// VEF storage where one resource unit is one canister, configurable for any one gas at a time. Since
/// <see cref="ThingComp.props"/> is shared by every spawned instance of the def, this comp clones it into an
/// instance-owned copy the first time it spawns, then mutates only that copy's <c>gas</c>/<c>pipeNet</c> when
/// <see cref="SetActiveGas"/> runs -- never the shared def-level object.
///
/// Additions to VEF's storage:
/// - the active gas is chosen by a pawn job (see JobDriver_ReconfigureManifold), not an instant toggle;
/// - switching gas ejects any racked canisters (full and a genuinely fractional partial, as real items -- see
///   Thing_GasCanister) and re-registers with the new gas's PipeNet;
/// - refilling from canisters is a custom job (WorkGiver_RefillManifold/JobDriver_RefillManifold), not VEF's
///   stock one, since that assumes every canister contributes a fixed flat amount -- ours can be partial;
/// - canister bodies are tracked, and drained ones are ejected as empty shells;
/// - deconstruction returns full canisters (and a partial, as above), and destruction bursts the stored gas.
/// </summary>
public sealed class CompGasManifold : CompResourceStorage
{
    private static readonly GasDef[] AllGases =
    {
        GVDefOf.GV_Gas_Toxin, GVDefOf.GV_Gas_Sedative, GVDefOf.GV_Gas_Haywire, GVDefOf.GV_Gas_Insecticide
    };

    private CompProperties_GasManifold instanceProps;
    private GasDef activeGas;
    private GasDef pendingGas;
    private int shellCount;

    public new CompProperties_GasManifold Props => instanceProps ?? (CompProperties_GasManifold)props;

    public GasDef ActiveGas => activeGas;

    /// <summary>Set by the reconfigure gizmo; cleared once a pawn finishes the job that applies it.</summary>
    public GasDef PendingGas => pendingGas;

    public int ShellCount => shellCount;

    /// <summary>Never registers with a network until a gas has been chosen.</summary>
    private bool suppressTransmitDuringUnregister;

    /// <summary>
    /// False while temporarily null (no gas configured), AND false while SetActiveGas is mid-unregister.
    /// The second case matters: PipeNetManager.UnregisterConnector rebuilds the *remaining* network via a
    /// neighbour flood-fill (PipeNetManager.CreatePipeNetFrom), which re-scans physically adjacent things --
    /// including this manifold, whose Props.pipeNet still reports the OLD gas at that point (SetActiveGas
    /// only updates it after UnregisterConnector returns). Since that flood-fill only skips a comp it finds
    /// when TransmitResourceNow is false, without this the manifold gets silently re-absorbed into the very
    /// network it's supposed to be leaving. Confirmed live: after switching gas twice, the previous gas's
    /// network still reported this manifold's stored amount.
    /// </summary>
    public override bool TransmitResourceNow => !suppressTransmitDuringUnregister && activeGas != null;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        EnsureInstanceProps();
        base.PostSpawnSetup(respawningAfterLoad);
        shellCount = Mathf.Max(shellCount, BodiesFor(AmountStored));
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Defs.Look(ref activeGas, "gvActiveGas");
        Scribe_Defs.Look(ref pendingGas, "gvPendingGas");
        Scribe_Values.Look(ref shellCount, "gvShellCount");
    }

    public override void CompTickRare()
    {
        base.CompTickRare();
        SyncShells();
    }

    /// <summary>Raises the body count when canisters were added; ejects shells for canisters fully drained.</summary>
    public void SyncShells()
    {
        int bodies = BodiesFor(AmountStored);
        if (bodies > shellCount)
        {
            shellCount = bodies;
            return;
        }
        if (bodies < shellCount && parent.Spawned)
        {
            int eject = shellCount - bodies;
            shellCount = bodies;
            Drop(GVDefOf.GV_CanisterEmpty, eject, DropCell(), parent.Map);
        }
    }

    /// <summary>Player-facing: queues a gas switch for a pawn to carry out (see <see cref="WorkGiver_ReconfigureManifold"/>),
    /// or applies it instantly in dev mode's God mode.</summary>
    [SyncMethod]
    public void RequestGasChange(GasDef newGas)
    {
        if (newGas == activeGas)
        {
            pendingGas = null;
            return;
        }
        // Dev mode + God mode: apply instantly, same as vanilla's own "skip requirements" debug behaviour,
        // so playtesting doesn't need to wait on a pawn every time a gas is switched.
        if (DebugSettings.godMode)
        {
            pendingGas = null;
            SetActiveGas(newGas);
            return;
        }
        pendingGas = newGas;
    }

    /// <summary>Called by JobDriver_ReconfigureManifold once the pawn's wait toil finishes.</summary>
    public void CompleteReconfigure()
    {
        if (pendingGas == null)
        {
            return;
        }
        GasDef newGas = pendingGas;
        pendingGas = null;
        SetActiveGas(newGas);
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo gizmo in base.CompGetGizmosExtra())
        {
            yield return gizmo;
        }
        if (parent.Faction != Faction.OfPlayer)
        {
            yield break;
        }
        yield return new Command_Action
        {
            defaultLabel = "GV_ManifoldGas".Translate(activeGas?.LabelCap ?? "GV_ManifoldGasNone".Translate()),
            defaultDesc = "GV_ManifoldGasDesc".Translate(),
            icon = GasVentTextures.GasSwatch,
            action = () => Find.WindowStack.Add(new FloatMenu(GasMenuOptions()))
        };
    }

    /// <summary>
    /// Deliberately does not call base.CompInspectStringExtra(): VEF's CompResourceStorage/CompResource chain
    /// prints the stored amount twice over in slightly different phrasing, plus (in dev mode) a raw
    /// PipeNet.ToString() dump of production/consumption/overflow figures our design never populates. Stored
    /// vs. capacity is the one number that actually matters here.
    /// </summary>
    public override string CompInspectStringExtra()
    {
        string line = activeGas == null
            ? "GV_ManifoldGasNone".Translate().ToString().CapitalizeFirst() + "."
            : "GV_ManifoldStored".Translate(activeGas.LabelCap, AmountStored.ToString("F1"), Props.storageCapacity.ToString("F0"));
        if (pendingGas != null)
        {
            line += "\n" + "GV_ManifoldReconfiguring".Translate(pendingGas.LabelCap);
        }
        return line;
    }

    private List<FloatMenuOption> GasMenuOptions()
    {
        List<FloatMenuOption> options = new List<FloatMenuOption>();
        for (int i = 0; i < AllGases.Length; i++)
        {
            GasDef gas = AllGases[i];
            bool isTarget = (pendingGas ?? activeGas) == gas;
            string label = (isTarget ? "[x] " : "[ ] ") + gas.LabelCap;
            options.Add(new FloatMenuOption(label, () => RequestGasChange(gas)));
        }
        return options;
    }

    /// <summary>Ejects any racked shells, bursts any stored gas, and rebinds to the new gas's network.</summary>
    [SyncMethod]
    public void SetActiveGas(GasDef newGas)
    {
        if (newGas == activeGas)
        {
            return;
        }
        bool spawned = parent.Spawned;
        Map map = parent.Map;
        if (spawned && activeGas != null)
        {
            EjectOnSwitch(map);
            suppressTransmitDuringUnregister = true;
            try
            {
                map.GetComponent<PipeNetManager>()?.UnregisterConnector(this);
            }
            finally
            {
                suppressTransmitDuringUnregister = false;
            }
        }
        activeGas = newGas;
        EnsureInstanceProps();
        ApplyActiveGasToProps();
        if (spawned)
        {
            shellCount = 0;
            if (TransmitResourceNow)
            {
                map.GetComponent<PipeNetManager>().RegisterConnector(this);
            }
        }
    }

    /// <summary>
    /// Drops the actual racked canisters, not a gas burst -- a canister is an ordinary item (stored, hauled,
    /// picked up) whether full, partially used or empty, and switching gas doesn't trigger it any more than
    /// taking it out of storage would. Mirrors PostDestroy's deconstruct handling exactly: full canisters of
    /// the old gas come out intact, and a genuinely fractional remainder comes out too, as a real partially
    /// filled canister (Thing_GasCanister.fill less than 1) rather than a burst.
    /// </summary>
    private void EjectOnSwitch(Map map)
    {
        float stored = AmountStored;
        int full = Mathf.FloorToInt(stored + 0.0001f);
        float partial = stored - full;
        bool hasPartial = partial > 0.001f;
        int emptyShells = shellCount - full - (hasPartial ? 1 : 0);
        IntVec3 cell = DropCell();
        if (full > 0)
        {
            Drop(activeGas.canister, full, cell, map);
        }
        if (emptyShells > 0)
        {
            Drop(GVDefOf.GV_CanisterEmpty, emptyShells, cell, map);
        }
        if (hasPartial)
        {
            DropPartialCanister(activeGas.canister, partial, cell, map);
        }
        Empty();
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        float stored = AmountStored;
        GasDef gas = activeGas;
        base.PostDestroy(mode, previousMap);
        if (previousMap == null || stored <= 0f || gas == null)
        {
            return;
        }
        IntVec3 position = parent.Position;
        switch (mode)
        {
            case DestroyMode.Deconstruct:
            case DestroyMode.Refund:
            {
                int full = Mathf.FloorToInt(stored + 0.0001f);
                float partial = stored - full;
                if (full > 0)
                {
                    Drop(gas.canister, full, position, previousMap);
                }
                if (partial > 0.001f)
                {
                    DropPartialCanister(gas.canister, partial, position, previousMap);
                }
                break;
            }
            case DestroyMode.KillFinalize:
            case DestroyMode.KillFinalizeLeavingsOnly:
                VentGasGrid.For(previousMap)?.ReleaseBurst(position, gas, Mathf.RoundToInt(stored * gas.densityPerCanister));
                break;
        }
    }

    private void EnsureInstanceProps()
    {
        if (instanceProps != null)
        {
            return;
        }
        CompProperties_GasManifold shared = (CompProperties_GasManifold)props;
        instanceProps = new CompProperties_GasManifold
        {
            compClass = shared.compClass,
            soundAmbient = shared.soundAmbient,
            storageCapacity = shared.storageCapacity,
            drawStorageBar = shared.drawStorageBar,
            addStorageInfo = shared.addStorageInfo,
            addTransferGizmo = shared.addTransferGizmo,
            showOffMatWhenTransfering = shared.showOffMatWhenTransfering,
            margin = shared.margin,
            barSize = shared.barSize,
            centerOffset = shared.centerOffset,
            barHorizontal = shared.barHorizontal,
            rotateBarWithBuilding = shared.rotateBarWithBuilding,
            extractOptions = shared.extractOptions,
            destroyOptions = shared.destroyOptions,
            contentRequirePower = shared.contentRequirePower,
            preventRotInNegativeTemp = shared.preventRotInNegativeTemp,
            daysToRotStart = shared.daysToRotStart
        };
        props = instanceProps;
        ApplyActiveGasToProps();
    }

    private void ApplyActiveGasToProps()
    {
        instanceProps.gas = activeGas;
        // CompResource.PostSpawnSetup calls RemovePipes() unconditionally (not gated on TransmitResourceNow),
        // and RemovePipes reads Props.pipeNet.pipeDefs without a null check -- so pipeNet must never actually
        // be null, even before a gas is chosen. TransmitResourceNow (activeGas != null) is what really gates
        // network registration/emission, so this placeholder is otherwise inert.
        instanceProps.pipeNet = activeGas?.pipeNet ?? GVDefOf.GV_Gas_Toxin.pipeNet;
    }

    private IntVec3 DropCell()
    {
        return parent.def.hasInteractionCell ? parent.InteractionCell : parent.Position;
    }

    private static int BodiesFor(float stored)
    {
        return Mathf.CeilToInt(stored - 0.0001f);
    }

    private static void Drop(ThingDef def, int count, IntVec3 cell, Map map)
    {
        Thing thing = ThingMaker.MakeThing(def);
        thing.stackCount = count;
        GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near);
    }

    private static void DropPartialCanister(ThingDef canisterDef, float fill, IntVec3 cell, Map map)
    {
        Thing_GasCanister thing = (Thing_GasCanister)ThingMaker.MakeThing(canisterDef);
        thing.fill = fill;
        GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near);
    }
}
