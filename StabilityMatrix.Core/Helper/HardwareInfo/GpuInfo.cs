namespace StabilityMatrix.Core.Helper.HardwareInfo;

public record GpuInfo
{
    public int Index { get; set; }
    public string? Name { get; init; } = string.Empty;
    public ulong MemoryBytes { get; init; }
    public MemoryLevel? MemoryLevel =>
        MemoryBytes switch
        {
            <= 0 => HardwareInfo.MemoryLevel.Unknown,
            < 4 * Size.GiB => HardwareInfo.MemoryLevel.Low,
            < 8 * Size.GiB => HardwareInfo.MemoryLevel.Medium,
            _ => HardwareInfo.MemoryLevel.High
        };

    public bool IsNvidia
    {
        get
        {
            var name = Name?.ToLowerInvariant();

            if (string.IsNullOrEmpty(name))
                return false;

            return name.Contains("nvidia") || name.Contains("tesla");
        }
    }

    public bool IsBlackwellGpu()
    {
        if (Name is null)
            return false;

        return IsNvidia
            && Name.Contains("RTX 50", StringComparison.OrdinalIgnoreCase)
            && !Name.Contains("RTX 5000", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsTritonCompatibleGpu()
    {
        if (Name is null)
            return false;

        return IsNvidia
            && Name.Contains("RTX", StringComparison.OrdinalIgnoreCase)
            && !Name.Contains("RTX 20")
            && !Name.Contains("RTX 4000")
            && !Name.Contains("RTX 5000")
            && !Name.Contains("RTX 6000")
            && !Name.Contains("RTX 8000");
    }

    public bool IsAmd => Name?.Contains("amd", StringComparison.OrdinalIgnoreCase) ?? false;
    public bool IsIntel => Name?.Contains("arc", StringComparison.OrdinalIgnoreCase) ?? false;

    public virtual bool Equals(GpuInfo? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return Name == other.Name && MemoryBytes == other.MemoryBytes;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Index, Name, MemoryBytes);
    }
}
