using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace GasVentilation;

/// <summary>
/// Added to every powered door (any mod's) at startup by <see cref="DoorLockInjector"/>. Carries no settings of
/// its own -- a door locks purely because a <see cref="CompIntruderSensor"/> links to it (see ITab_GasSensor)
/// and is currently triggered. <see cref="GasDeviceRegistry"/> recomputes <see cref="lockedTargets"/> for every
/// registered door on the same cadence it already evaluates sensors (CompIntruderSensor.Evaluate), so this never
/// does its own per-tick work.
///
/// Blocking who can OPEN the door (Patch_Door_PawnCanOpen) is not enough by itself: Building_Door.CanPhysicallyPass
/// -- the method pathing actually calls -- skips PawnCanOpen entirely whenever the door's own FreePassage is true,
/// which "Hold Open" forces permanently true. So locking also suppresses Hold Open for as long as the lock holds,
/// exactly as if the player toggled it off themselves, and restores their original setting the moment it clears.
///
/// Suppressing Hold Open alone isn't enough either: vanilla only resumes its own close countdown once a friendly
/// pawn has touched the door in the last 2 seconds (Building_Door.CanTryCloseAutomatically), so a door that's sat
/// open for a while (nobody walking past it right when the lock engages) would just stay open forever waiting for
/// that. So engaging the lock also force-calls the door's own DoorTryClose() once, immediately, the same close
/// vanilla would eventually do itself -- it still politely refuses if a pawn is literally standing in the doorway.
/// </summary>
public sealed class CompDoorLock : ThingComp
{
    private static readonly Material LockedMat = MaterialPool.MatFrom(GasVentTextures.DoorLocked, ShaderDatabase.MetaOverlay, Color.white);

    private static readonly AccessTools.FieldRef<Building_Door, bool> HoldOpenField =
        AccessTools.FieldRefAccess<Building_Door, bool>("holdOpenInt");

    private static readonly MethodInfo DoorTryCloseMethod = AccessTools.Method(typeof(Building_Door), "DoorTryClose");

    /// <summary>The union of Targets across every currently-triggered sensor linked to this door. None means
    /// unlocked. Not saved -- GasDeviceRegistry recomputes it within one SensorInterval (60 ticks) of load.</summary>
    private SensorTargets lockedTargets;

    /// <summary>Whether this lock is the one currently holding Hold Open off, and what to restore it to. Saved,
    /// since a save/load mid-lock would otherwise lose the player's original Hold Open setting forever: the
    /// restore-on-unlock path only runs if the same comp instance that suppressed it is still the one watching.</summary>
    private bool suppressingHoldOpen;
    private bool savedHoldOpen;

    public bool Locked => lockedTargets != SensorTargets.None;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        VentGasGrid.For(parent.Map)?.Devices.RegisterDoorLock(this);
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        base.PostDeSpawn(map, mode);
        lockedTargets = SensorTargets.None;
        VentGasGrid.For(map)?.Devices.DeregisterDoorLock(this);
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref suppressingHoldOpen, "gvSuppressingHoldOpen");
        Scribe_Values.Look(ref savedHoldOpen, "gvSavedHoldOpen");
    }

    /// <summary>Called by GasDeviceRegistry every SensorInterval ticks.</summary>
    internal void SetLockedTargets(SensorTargets targets)
    {
        bool wasLocked = Locked;
        lockedTargets = targets;
        bool isLocked = Locked;
        if (isLocked)
        {
            // Re-asserted every evaluation pass, not just on the locked/unlocked transition: the gizmo is
            // disabled while locked (Patch_Door_GetGizmos), but a direct toggle call (Dev Mode, Multiplayer
            // replay, another mod) could otherwise flip Hold Open back on mid-lock and silently defeat it.
            EngageHoldOpenSuppression();
        }
        else if (wasLocked)
        {
            ReleaseHoldOpenSuppression();
        }
    }

    private void EngageHoldOpenSuppression()
    {
        if (!(parent is Building_Door door))
        {
            return;
        }
        if (!suppressingHoldOpen && door.HoldOpen)
        {
            savedHoldOpen = true;
            suppressingHoldOpen = true;
        }
        if (suppressingHoldOpen)
        {
            HoldOpenField(door) = false;
        }
        // Harmless to retry every pass: DoorTryClose() is a no-op once the door is already closed, and simply
        // declines (returns false) if a pawn is standing in the doorway -- we just try again next evaluation.
        if (door.Open)
        {
            DoorTryCloseMethod.Invoke(door, null);
        }
    }

    private void ReleaseHoldOpenSuppression()
    {
        if (!suppressingHoldOpen)
        {
            return;
        }
        suppressingHoldOpen = false;
        if (parent is Building_Door door)
        {
            HoldOpenField(door) = savedHoldOpen;
        }
    }

    /// <summary>Whether this door, in its current locked state, blocks this specific pawn. Reuses the exact
    /// same matching a sensor uses to decide what trips it: a door blocks whichever pawns its linking sensor(s)
    /// would detect, nothing more.</summary>
    public bool Blocks(Pawn pawn)
    {
        return Locked && CompIntruderSensor.MatchesTargets(pawn, lockedTargets);
    }

    public override string CompInspectStringExtra()
    {
        return Locked ? "GV_DoorLockedInspect".Translate() : null;
    }

    public override void PostDraw()
    {
        base.PostDraw();
        if (!Locked)
        {
            return;
        }
        Vector3 pos = parent.TrueCenter();
        pos.y = AltitudeLayer.MetaOverlays.AltitudeFor();
        Graphics.DrawMesh(MeshPool.plane08, Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one), LockedMat, 0);
    }
}
