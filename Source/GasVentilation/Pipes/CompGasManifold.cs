using PipeSystem;
using UnityEngine;
using Verse;

namespace GasVentilation;

/// <summary>
/// VEF storage where one resource unit is one canister. Additions to VEF's storage:
/// - pawn refill is enabled by default on newly built manifolds;
/// - the refill registration is cleaned up on despawn (VEF's toggle otherwise drops reinstalled manifolds);
/// - canister bodies are tracked, and drained ones are ejected as empty shells;
/// - deconstruction returns full canisters, and destruction bursts the stored gas.
/// </summary>
public sealed class CompGasManifold : CompResourceStorage
{
    private int shellCount;

    public new CompProperties_GasManifold Props => (CompProperties_GasManifold)props;

    public int ShellCount => shellCount;

    public override void PostPostMake()
    {
        base.PostPostMake();
        markedForRefill = true;
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        shellCount = Mathf.Max(shellCount, BodiesFor(AmountStored));
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        base.PostDeSpawn(map, mode);
        map.GetComponent<PipeNetManager>()?.wantRefill.Remove(parent);
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
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

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        float stored = AmountStored;
        base.PostDestroy(mode, previousMap);
        if (previousMap == null || stored <= 0f)
        {
            return;
        }
        GasDef gas = Props.gas;
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
                    Drop(Props.refillOptions.thing, full, position, previousMap);
                }
                if (partial > 0.001f)
                {
                    Drop(GVDefOf.GV_CanisterEmpty, 1, position, previousMap);
                    VentGasGrid.For(previousMap)?.ReleaseBurst(position, gas, Mathf.RoundToInt(partial * gas.densityPerCanister));
                }
                break;
            }
            case DestroyMode.KillFinalize:
            case DestroyMode.KillFinalizeLeavingsOnly:
                VentGasGrid.For(previousMap)?.ReleaseBurst(position, gas, Mathf.RoundToInt(stored * gas.densityPerCanister));
                break;
        }
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
}
