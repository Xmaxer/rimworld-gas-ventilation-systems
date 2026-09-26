using System.Collections.Generic;
using Verse;

namespace GasVentilation;

/// <summary>
/// Which gases a pawn should avoid, cached for 250 ticks. Path requests and danger checks are frequent, and the
/// mask needs stat reads. Main thread only: off-thread callers get an empty mask and never touch the cache.
/// </summary>
public static class GasPawnMaskCache
{
    private const int StaleTicks = 250;
    private const int MaxEntries = 2048;

    private static readonly Dictionary<int, long> Cache = new Dictionary<int, long>();
    private static Game cachedGame;

    public static byte MaskFor(Pawn pawn)
    {
        if (!UnityData.IsInMainThread)
        {
            return 0;
        }
        if (Current.Game != cachedGame)
        {
            Cache.Clear();
            cachedGame = Current.Game;
        }
        int now = Find.TickManager.TicksGame;
        if (Cache.TryGetValue(pawn.thingIDNumber, out long entry))
        {
            int tick = (int)(entry >> 8);
            if (now - tick < StaleTicks && now >= tick)
            {
                return (byte)(entry & 0xFF);
            }
        }
        if (Cache.Count >= MaxEntries)
        {
            Cache.Clear();
        }
        byte mask = GasExposure.RelevantMask(pawn);
        Cache[pawn.thingIDNumber] = ((long)now << 8) | mask;
        return mask;
    }

    /// <summary>Drops the cached mask so the next query recomputes it (e.g. after apparel changes in tests).</summary>
    public static void Invalidate(Pawn pawn)
    {
        if (UnityData.IsInMainThread)
        {
            Cache.Remove(pawn.thingIDNumber);
        }
    }
}
