using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Python;

public class UvPythonListEntry
{
    public required string Key { get; set; }
    public required string Version { get; set; }
    public string? Path { get; set; }
    public string? Symlink { get; set; }
    public Uri? Url { get; set; }
    public string Os { get; set; }
    public string Variant { get; set; }
    public string Implementation { get; set; }
    public string Arch { get; set; }
    public string Libc { get; set; }

    [JsonIgnore]
    public PyVersion VersionParts
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Version))
                return new PyVersion(0, 0, 0);

            if (Version.Contains("a"))
            {
                // substring to exclude everything after the first "a" (including the first "a")
                var version = Version.Substring(0, Version.IndexOf("a", StringComparison.OrdinalIgnoreCase));
                return PyVersion.Parse(version);
            }

            if (Version.Contains("b"))
            {
                // substring to exclude everything after the first "b" (including the first "b")
                var version = Version.Substring(0, Version.IndexOf("b", StringComparison.OrdinalIgnoreCase));
                return PyVersion.Parse(version);
            }

            if (Version.Contains("rc"))
            {
                // substring to exclude everything after the first "rc" (including the first "rc")
                var version = Version.Substring(0, Version.IndexOf("rc", StringComparison.OrdinalIgnoreCase));
                return PyVersion.Parse(version);
            }

            return PyVersion.Parse(Version);
        }
    }

    [JsonIgnore]
    public bool IsPrerelease => Version.Contains("a") || Version.Contains("b") || Version.Contains("rc");
}
