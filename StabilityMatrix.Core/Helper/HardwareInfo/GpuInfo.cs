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

        // Normalize for safer substring checks (handles RX7800 vs RX 7800, etc.)
        var name = Name;
        var nameNoSpaces = name.Replace(" ", "", StringComparison.Ordinal);

        return name switch
        {
            // RDNA4
            _ when Has("R9700") || Has("9070") => "gfx1201",
            _ when Has("9060") => "gfx1200",

            // RDNA3.5 APUs
            _ when Has("860M") => "gfx1152",
            _ when Has("890M") => "gfx1150",
            _ when Has("8040S") || Has("8050S") || Has("8060S") || Has("880M") || Has("Z2 Extreme") =>
                "gfx1151",

            // RDNA3 APUs (Phoenix)
            _ when Has("740M") || Has("760M") || Has("780M") || Has("Z1") || Has("Z2") => "gfx1103",

            // RDNA3 dGPU Navi33
            _ when Has("7400") || Has("7500") || Has("7600") || Has("7650") || Has("7700S") => "gfx1102",

            // RDNA3 dGPU Navi32
            _ when Has("7700") || Has("RX 7800") || HasNoSpace("RX7800") => "gfx1101",

            // RDNA3 dGPU Navi31 (incl. Pro)
            _ when Has("W7800") || Has("7900") || Has("7950") || Has("7990") => "gfx1100",

            // RDNA2 APUs (Rembrandt)
            _ when Has("660M") || Has("680M") => "gfx1035",

            // RDNA2 Navi24 low-end (incl. some mobiles)
            _ when Has("6300") || Has("6400") || Has("6450") || Has("6500") || Has("6550") || Has("6500M") =>
                "gfx1034",

            // RDNA2 Navi23
            _ when Has("6600") || Has("6650") || Has("6700S") || Has("6800S") || Has("6600M") => "gfx1032",

            // RDNA2 Navi22 (note: desktop 6800 is NOT here; that’s Navi21/gfx1030)
            _ when Has("6700") || Has("6750") || Has("6800M") || Has("6850M") => "gfx1031",

            // RDNA2 Navi21 (big die)
            _ when Has("6800") || Has("6900") || Has("6950") => "gfx1030",

            _ => null,
        };

        bool HasNoSpace(string s) =>
            nameNoSpaces.Contains(
                s.Replace(" ", "", StringComparison.Ordinal),
                StringComparison.OrdinalIgnoreCase
            );
        bool Has(string s) => name.Contains(s, StringComparison.OrdinalIgnoreCase);
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
