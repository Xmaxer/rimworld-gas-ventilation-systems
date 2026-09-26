namespace GasVentilation.Core;

/// <summary>Four gas densities (0-255) packed into one uint, one byte per channel (channel 0 = lowest byte).</summary>
public static class GasPacking
{
    public const int Channels = 4;
    public const int MaxDensity = 255;

    public static int Get(uint packed, int channel)
    {
        return (int)((packed >> (channel << 3)) & 0xFFu);
    }

    public static uint With(uint packed, int channel, int density)
    {
        if (density < 0)
        {
            density = 0;
        }
        else if (density > MaxDensity)
        {
            density = MaxDensity;
        }
        int shift = channel << 3;
        return (packed & ~(0xFFu << shift)) | ((uint)density << shift);
    }

    /// <summary>Highest density among the channels whose bit is set in <paramref name="channelMask"/>.</summary>
    public static int MaxMaskedDensity(uint packed, int channelMask)
    {
        int max = 0;
        for (int channel = 0; channel < Channels; channel++)
        {
            if ((channelMask & (1 << channel)) != 0)
            {
                int density = Get(packed, channel);
                if (density > max)
                {
                    max = density;
                }
            }
        }
        return max;
    }
}
