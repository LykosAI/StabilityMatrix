using System.Text.Json.Serialization;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Core.Models.Update;

public record UpdatePlatforms
{
    [JsonPropertyName("win-x64")]
    public UpdateInfo? WindowsX64 { get; init; }

    [JsonPropertyName("linux-x64")]
    public UpdateInfo? LinuxX64 { get; init; }

    [JsonPropertyName("macos-arm64")]
    public UpdateInfo? MacOsArm64 { get; init; }

    public UpdateInfo? GetInfoForCurrentPlatform()
    {
        if (Compat.IsWindows)
        {
            return WindowsX64;
        }

        if (Compat.IsLinux)
        {
            return LinuxX64;
        }

        if (Compat.IsMacOS && Compat.IsArm)
        {
            return MacOsArm64;
        }

        return null;
    }
}
