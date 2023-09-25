using System;

namespace StabilityMatrix.Avalonia.Models.Inference;

[Flags]
public enum GenerateFlags
{
    None = 0,
    HiresFixEnable = 1 << 1,
    HiresFixDisable = 1 << 2,
    UseCurrentSeed = 1 << 3,
    UseRandomSeed = 1 << 4,
    HiresFixAndUseCurrentSeed = HiresFixEnable | UseCurrentSeed,
}
