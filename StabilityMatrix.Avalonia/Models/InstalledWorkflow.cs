using System.Collections.Generic;
using System.Text.Json.Serialization;
using Avalonia.Platform.Storage;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Models;

/// <summary>
/// A workflow file in the installed workflows library, deserialized from the
/// <see cref="WorkflowMetadata"/> embedded in the file.
/// </summary>
public class InstalledWorkflow : WorkflowMetadata
{
    [JsonIgnore]
    public List<IStorageFile>? FilePath { get; set; }

    /// <summary>
    /// Top-level subfolder of the workflow library this file lives in,
    /// or an empty string for files directly in the library root.
    /// </summary>
    [JsonIgnore]
    public string Folder { get; set; } = string.Empty;

    /// <summary>
    /// Path of a local <c>{name}.preview.{ext}</c> sidecar image, when one exists.
    /// </summary>
    [JsonIgnore]
    public string? LocalPreviewPath { get; set; }

    /// <summary>
    /// Thumbnail to display: a local sidecar image wins over the embedded thumbnail url.
    /// </summary>
    [JsonIgnore]
    public string? DisplayThumbnail => LocalPreviewPath ?? FirstThumbnail;

    [JsonIgnore]
    internal int Index { get; set; }
}
