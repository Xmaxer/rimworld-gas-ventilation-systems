using System;

namespace GasVentilation.Core.Tests;

internal sealed class TestEnvironment : IGasEnvironment
{
    private readonly Random rng;

    public TestEnvironment(int cellCount, int seed = 1)
    {
        Blocked = new bool[cellCount];
        Roofed = new bool[cellCount];
        rng = new Random(seed);
    }

    public bool[] Blocked { get; }

    public bool[] Roofed { get; }

    public float VacuumEverywhere { get; set; }

    public bool CanHoldGas(int cellIndex) => !Blocked[cellIndex];

    public bool IsRoofed(int cellIndex) => Roofed[cellIndex];

    public float Vacuum(int cellIndex) => VacuumEverywhere;

    public int RandomInt(int exclusiveMax) => rng.Next(exclusiveMax);

    public static GasChannelSettings[] Settings(float dissipation, bool diffuses, float roofedFactor = 0.5f, int minDiffusion = 17)
    {
        GasChannelSettings s = new GasChannelSettings
        {
            DissipationPerVisit = dissipation,
            RoofedFactor = roofedFactor,
            VacuumFactor = 25f,
            MinDiffusion = minDiffusion,
            Diffuses = diffuses
        };
        return new[] { s, s, s, s };
    }
}
