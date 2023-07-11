namespace StabilityMatrix.Core.Helper;

[Flags]
public enum PlatformKind
{
    Unknown = 0,
    Windows = 1 << 0,
    Unix = 1 << 1,
    Linux = Unix | 1 << 2,
    MacOS = Unix | 1 << 3,
}
