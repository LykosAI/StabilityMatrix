namespace StabilityMatrix.Core.Helper.HardwareInfo;

public record GpuInfo
{
    public int Index { get; set; }
    public string? Name { get; init; } = string.Empty;
    public ulong MemoryBytes { get; init; }
    public string? ComputeCapability { get; init; }

    /// <summary>
    /// Gets the compute capability as a comparable decimal value (e.g. "8.6" becomes 8.6m)
    /// Returns null if compute capability is not available
    /// </summary>
    public decimal? ComputeCapabilityValue =>
        ComputeCapability != null && decimal.TryParse(ComputeCapability, out var value) ? value : null;

    public MemoryLevel? MemoryLevel =>
        MemoryBytes switch
        {
            <= 0 => HardwareInfo.MemoryLevel.Unknown,
            < 4 * Size.GiB => HardwareInfo.MemoryLevel.Low,
            < 8 * Size.GiB => HardwareInfo.MemoryLevel.Medium,
            _ => HardwareInfo.MemoryLevel.High,
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
        if (ComputeCapability is null)
            return false;

        return ComputeCapabilityValue >= 12.0m;
    }

    public bool IsAmpereOrNewerGpu()
    {
        if (ComputeCapability is null)
            return false;

        return ComputeCapabilityValue >= 8.6m;
    }

    public bool IsLegacyNvidiaGpu()
    {
        if (ComputeCapability is null)
            return false;

        return ComputeCapabilityValue < 7.5m;
    }

    public bool IsWindowsRocmSupportedGpu()
    {
        var gfx = GetAmdGfxArch();
        if (gfx is null)
            return false;

        return gfx.StartsWith("gfx110") || gfx.StartsWith("gfx120") || gfx.Equals("gfx1151");
    }

    public bool IsAmd => Name?.Contains("amd", StringComparison.OrdinalIgnoreCase) ?? false;
    public bool IsIntel => Name?.Contains("arc", StringComparison.OrdinalIgnoreCase) ?? false;

    public string? GetAmdGfxArch()
    {
        if (!IsAmd || string.IsNullOrWhiteSpace(Name))
            return null;

        var name = Name.ToLowerInvariant();

        if (name.Contains("9070") || name.Contains("R9700"))
            return "gfx1201";

        if (name.Contains("9060"))
            return "gfx1200";

        if (name.Contains("z2") || name.Contains("880m") || name.Contains("8050s") || name.Contains("8060s"))
            return "gfx1151";

        if (name.Contains("740m") || name.Contains("760m") || name.Contains("780m") || name.Contains("z1"))
            return "gfx1103";

        if (
            name.Contains("w7400")
            || name.Contains("w7500")
            || name.Contains("w7600")
            || name.Contains("7500 xt")
            || name.Contains("7600")
            || name.Contains("7650 gre")
            || name.Contains("7700s")
        )
            return "gfx1102";

        if (
            name.Contains("v710")
            || name.Contains("7700")
            || (name.Contains("7800") && !name.Contains("w7800"))
        )
            return "gfx1101";

        if (name.Contains("w7800") || name.Contains("7900") || name.Contains("7950") || name.Contains("7990"))
            return "gfx1100";

        return null;
    }

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
