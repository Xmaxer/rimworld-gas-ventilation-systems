using System;

namespace GasVentilation;

[Flags]
public enum GasTargetFlags : byte
{
    None = 0,
    Organic = 1,
    Breathes = 2,
    Mechanical = 4,
    Insectoid = 8,
    Synthetic = 16,
    Excluded = 128
}

public enum GasTargetCategory
{
    Auto,
    Organic,
    Mechanical,
    Insectoid,
    Synthetic,
    Excluded
}

public enum GasBreathing
{
    Auto,
    Yes,
    No
}
