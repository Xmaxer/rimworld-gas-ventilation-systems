using NUnit.Framework;

namespace GasVentilation.Core.Tests;

public class SmokeTests
{
    [Test]
    public void CoreAssemblyIsReferenced()
    {
        Assert.That(CoreAssemblyMarker.Name, Is.EqualTo("GasVentilation.Core"));
    }
}
