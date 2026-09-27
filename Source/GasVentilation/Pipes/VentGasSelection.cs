using System;

namespace GasVentilation;

/// <summary>Which of the four gases a vent currently outputs. Any combination, output simultaneously.</summary>
[Flags]
public enum VentGasSelection
{
    None = 0,
    Toxin = 1,
    Sedative = 2,
    Haywire = 4,
    Insecticide = 8
}
