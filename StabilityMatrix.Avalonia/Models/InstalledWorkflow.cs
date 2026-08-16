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

    [JsonIgnore]
    internal int Index { get; set; }
}
