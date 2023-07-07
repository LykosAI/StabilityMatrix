namespace StabilityMatrix.Core.ReparsePoints;

[Flags]
internal enum Win32FileShare : uint
{
    None = 0x00000000,
    Read = 0x00000001,
    Write = 0x00000002,
    Delete = 0x00000004,
}
