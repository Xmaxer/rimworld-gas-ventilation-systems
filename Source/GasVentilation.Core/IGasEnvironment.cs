namespace GasVentilation.Core;

/// <summary>What the simulator needs to know about the world. Implemented by the RimWorld adapter and by tests.</summary>
public interface IGasEnvironment
{
    /// <summary>False for cells blocked by full-fillage buildings (walls, closed doors).</summary>
    bool CanHoldGas(int cellIndex);

    bool IsRoofed(int cellIndex);

    /// <summary>0..1 vacuum level (Odyssey); 0 when unsupported.</summary>
    float Vacuum(int cellIndex);

    /// <summary>Uniform integer in [0, exclusiveMax). Must be the game's deterministic RNG in-game.</summary>
    int RandomInt(int exclusiveMax);
}
