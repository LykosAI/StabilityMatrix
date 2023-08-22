using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models;

[JsonDerivedType(typeof(CivitPostDownloadContextAction), "CivitPostDownload")]
public interface IContextAction
{
    object? Context { get; set; }
}
