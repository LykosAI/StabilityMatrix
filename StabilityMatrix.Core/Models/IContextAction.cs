using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models;

[JsonDerivedType(typeof(CivitPostDownloadContextAction), "CivitPostDownload")]
[JsonDerivedType(typeof(ModelPostDownloadContextAction), "ModelPostDownload")]
[JsonDerivedType(typeof(GenericPostDownloadAction), "GenericPostDownload")]
public interface IContextAction
{
    object? Context { get; set; }
}
