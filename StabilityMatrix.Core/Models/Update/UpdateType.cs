namespace StabilityMatrix.Core.Models.Update;

[Flags]
public enum UpdateType
{
    Normal = 1 << 0,
    Critical = 1 << 1,
    Mandatory = 1 << 2,
}
