using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Text;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Inference;

namespace StabilityMatrix.Avalonia.Models.Inference;

/// <summary>
/// Event args for applying a <see cref="IComfyStep"/>.
/// </summary>
public class ModuleApplyStepEventArgs : EventArgs
{
    public required ComfyNodeBuilder Builder { get; init; }

    public NodeDictionary Nodes => Builder.Nodes;

    public ModuleApplyStepTemporaryArgs Temp { get; set; } = new();

    /// <summary>
    /// Generation overrides (like hires fix generate, current seed generate, etc.)
    /// </summary>
    public IReadOnlyDictionary<Type, bool> IsEnabledOverrides { get; init; } = new Dictionary<Type, bool>();

    public List<(string SourcePath, string DestinationRelativePath)> FilesToTransfer { get; init; } = [];

    /// <summary>
    /// Creates a new <see cref="ModuleApplyStepEventArgs"/> with the given <see cref="ComfyNodeBuilder"/>.
    /// </summary>
    /// <returns></returns>
    public ModuleApplyStepTemporaryArgs CreateTempFromBuilder()
    {
        return new ModuleApplyStepTemporaryArgs
        {
            Primary = Builder.Connections.Primary,
            PrimaryVAE = Builder.Connections.PrimaryVAE,
            Models = Builder.Connections.Models
        };
    }

    public void AddFileTransfer(string sourcePath, string destinationRelativePath)
    {
        FilesToTransfer.Add((sourcePath, destinationRelativePath));
    }

    /// <summary>
    /// Adds a file transfer to `models/configs`
    /// </summary>
    /// <returns>The destination relative path</returns>
    public string AddFileTransferToConfigs(string sourcePath)
    {
        // To avoid conflicts, we'll add the file name's crc32 before the extension
        var sourceNameWithoutExtension = Path.GetFileNameWithoutExtension(sourcePath);
        var sourceExtension = Path.GetExtension(sourcePath);
        var sourceNameCrc = Crc32.Hash(Encoding.UTF8.GetBytes(sourceNameWithoutExtension));
        var sourceNameCrcShort = BitConverter
            .ToString(sourceNameCrc)
            .ToLowerInvariant()
            .Replace("-", string.Empty)[..4];

        var destFileName = $"{sourceNameWithoutExtension}_{sourceNameCrcShort}{sourceExtension}";

        var destPath = Path.Combine("models", "configs", destFileName);

        FilesToTransfer.Add((sourcePath, destPath));

        return destPath;
    }
}
