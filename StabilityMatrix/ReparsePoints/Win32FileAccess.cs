using System;

namespace StabilityMatrix.ReparsePoints;

[Flags]
internal enum Win32FileAccess : uint
{
    GenericRead = 0x80000000U,
    GenericWrite = 0x40000000U,
    GenericExecute = 0x20000000U,
    GenericAll = 0x10000000U
}
