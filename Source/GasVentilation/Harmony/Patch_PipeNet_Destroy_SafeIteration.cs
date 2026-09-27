using System.Collections.Generic;
using HarmonyLib;
using PipeSystem;

namespace GasVentilation;

/// <summary>
/// Fixes a real bug in VEF's own PipeSystem.PipeNet.Destroy(): it iterates <c>connectors</c> by index while
/// UnregisterComp(connector) removes from that same list on every call, so every other connector is skipped
/// (removing index 0 shifts index 1 into index 0, but the loop advances to index 1, silently skipping it).
/// A skipped connector never gets its own PipeNet reference cleared and is left in a stale/destroyed network
/// -- normal VEF usage rarely hits this (a building's resource is usually fixed for life, so a whole network
/// most often empties out wholesale via despawn, not repeated partial unregistration), but our manifold's
/// runtime gas switching calls UnregisterConnector -> Destroy() on every switch, exactly the pattern that
/// triggers it, causing gas to appear to "leak" between networks after a couple of switches.
/// Confirmed live: a manifold switched Toxin -> Haywire -> Sedative ended up simultaneously counted as
/// stored in the Toxin, Haywire and Sedative networks at once (see docs/implementation-notes.md).
/// </summary>
[HarmonyPatch(typeof(PipeNet), nameof(PipeNet.Destroy))]
internal static class Patch_PipeNet_Destroy_SafeIteration
{
    [HarmonyPrefix]
    private static bool Prefix(PipeNet __instance)
    {
        __instance.map.GetComponent<PipeNetManager>().pipeNets.Remove(__instance);
        List<CompResource> snapshot = new List<CompResource>(__instance.connectors);
        for (int i = 0; i < snapshot.Count; i++)
        {
            __instance.UnregisterComp(snapshot[i]);
        }
        return false;
    }
}
