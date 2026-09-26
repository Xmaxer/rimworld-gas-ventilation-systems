using NUnit.Framework;

namespace GasVentilation.Core.Tests;

public class GasPackingTests
{
    [Test]
    public void Get_ReadsEachChannelFromItsByte()
    {
        const uint packed = 0x04030201u;
        Assert.That(GasPacking.Get(packed, 0), Is.EqualTo(1));
        Assert.That(GasPacking.Get(packed, 1), Is.EqualTo(2));
        Assert.That(GasPacking.Get(packed, 2), Is.EqualTo(3));
        Assert.That(GasPacking.Get(packed, 3), Is.EqualTo(4));
    }

    [Test]
    public void With_ReplacesOnlyTheTargetChannel()
    {
        uint packed = GasPacking.With(0x04030201u, 2, 200);
        Assert.That(packed, Is.EqualTo(0x04C80201u));
    }

    [Test]
    public void With_ClampsToByteRange()
    {
        Assert.That(GasPacking.Get(GasPacking.With(0u, 1, -5), 1), Is.EqualTo(0));
        Assert.That(GasPacking.Get(GasPacking.With(0u, 1, 300), 1), Is.EqualTo(255));
    }
}
