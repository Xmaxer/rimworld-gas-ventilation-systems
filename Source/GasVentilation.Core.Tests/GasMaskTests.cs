using NUnit.Framework;

namespace GasVentilation.Core.Tests;

public class GasMaskTests
{
    private static uint Packed(int c0, int c1, int c2, int c3)
    {
        return GasPacking.With(GasPacking.With(GasPacking.With(GasPacking.With(0u, 0, c0), 1, c1), 2, c2), 3, c3);
    }

    [Test]
    public void MaxMaskedDensity_OnlyConsidersMaskedChannels()
    {
        uint packed = Packed(10, 200, 50, 0);
        Assert.That(GasPacking.MaxMaskedDensity(packed, 0b0001), Is.EqualTo(10));
        Assert.That(GasPacking.MaxMaskedDensity(packed, 0b0101), Is.EqualTo(50));
        Assert.That(GasPacking.MaxMaskedDensity(packed, 0b1111), Is.EqualTo(200));
        Assert.That(GasPacking.MaxMaskedDensity(packed, 0), Is.EqualTo(0));
    }

    [Test]
    public void AnyBandAtLeast_ChecksMaskedChannelsOnly()
    {
        uint packed = Packed(10, 200, 0, 90);
        Assert.That(GasBands.AnyBandAtLeast(packed, 0b0001, GasBands.Low), Is.True);
        Assert.That(GasBands.AnyBandAtLeast(packed, 0b0001, GasBands.Medium), Is.False);
        Assert.That(GasBands.AnyBandAtLeast(packed, 0b1000, GasBands.Medium), Is.True);
        Assert.That(GasBands.AnyBandAtLeast(packed, 0b0100, GasBands.Low), Is.False);
        Assert.That(GasBands.AnyBandAtLeast(packed, 0b0010, GasBands.High), Is.True);
    }
}
