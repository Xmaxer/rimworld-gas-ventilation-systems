namespace GasVentilation.Core;

/// <summary>
/// Coarse density bands shared by rendering (cloud opacity) and AI path costs.
/// Two bits per channel, so all four channels fit in one byte.
/// </summary>
public static class GasBands
{
    public const int None = 0;
    public const int Low = 1;
    public const int Medium = 2;
    public const int High = 3;

    public const int MediumThreshold = 85;
    public const int HighThreshold = 170;

    public static int Band(int density)
    {
        if (density <= 0)
        {
            return None;
        }
        if (density < MediumThreshold)
        {
            return Low;
        }
        return density < HighThreshold ? Medium : High;
    }

    public static byte Pack(uint packed)
    {
        int result = 0;
        for (int channel = 0; channel < GasPacking.Channels; channel++)
        {
            result |= Band(GasPacking.Get(packed, channel)) << (channel << 1);
        }
        return (byte)result;
    }

    public static int Unpack(byte packedBands, int channel)
    {
        return (packedBands >> (channel << 1)) & 3;
    }

    /// <summary>True if any channel in <paramref name="channelMask"/> is at <paramref name="band"/> or higher.</summary>
    public static bool AnyBandAtLeast(uint packed, int channelMask, int band)
    {
        for (int channel = 0; channel < GasPacking.Channels; channel++)
        {
            if ((channelMask & (1 << channel)) != 0 && Band(GasPacking.Get(packed, channel)) >= band)
            {
                return true;
            }
        }
        return false;
    }
}
