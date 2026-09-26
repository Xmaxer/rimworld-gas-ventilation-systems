using NUnit.Framework;

namespace GasVentilation.Core.Tests;

public class GasBandsTests
{
    [TestCase(0, GasBands.None)]
    [TestCase(1, GasBands.Low)]
    [TestCase(84, GasBands.Low)]
    [TestCase(85, GasBands.Medium)]
    [TestCase(169, GasBands.Medium)]
    [TestCase(170, GasBands.High)]
    [TestCase(255, GasBands.High)]
    public void Band_UsesThresholds(int density, int expected)
    {
        Assert.That(GasBands.Band(density), Is.EqualTo(expected));
    }

    [Test]
    public void Pack_StoresTwoBitsPerChannel()
    {
        uint packed = GasPacking.With(GasPacking.With(0u, 0, 10), 3, 200);
        byte bands = GasBands.Pack(packed);
        Assert.That(GasBands.Unpack(bands, 0), Is.EqualTo(GasBands.Low));
        Assert.That(GasBands.Unpack(bands, 1), Is.EqualTo(GasBands.None));
        Assert.That(GasBands.Unpack(bands, 2), Is.EqualTo(GasBands.None));
        Assert.That(GasBands.Unpack(bands, 3), Is.EqualTo(GasBands.High));
    }
}
