using System.Collections.Generic;

namespace StabilityMatrix.Models;

public class LicenseInfo
{
    public string PackageName { get; set; }
    public string PackageVersion { get; set; }
    public string PackageUrl { get; set; }
    public string Copyright { get; set; }
    public List<string> Authors { get; set; }
    public string Description { get; set; }
    public string LicenseUrl { get; set; }
    public string LicenseType { get; set; }
}
