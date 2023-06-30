using System;

namespace StabilityMatrix.Updater;

[Flags]
public enum UpdateType
{
    Normal = 1 << 0,
    Critical = 1 << 1,
    Mandatory = 1 << 2,
}
