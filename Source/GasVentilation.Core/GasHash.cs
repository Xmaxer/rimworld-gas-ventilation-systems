namespace GasVentilation.Core;

/// <summary>Deterministic integer hash used for fractional dissipation (no RNG state, Multiplayer-safe).</summary>
internal static class GasHash
{
    public static uint Mix(int a, int b, int c)
    {
        unchecked
        {
            uint h = (uint)a * 0x9E3779B1u;
            h ^= (uint)b * 0x85EBCA77u;
            h ^= (uint)c * 0xC2B2AE3Du;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return h;
        }
    }
}
