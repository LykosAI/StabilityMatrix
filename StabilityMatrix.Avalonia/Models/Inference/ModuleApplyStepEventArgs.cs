using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Text;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

namespace StabilityMatrix.Avalonia.Models.Inference;

/// <summary>
/// Event args for applying a <see cref="IComfyStep"/>.
/// </summary>
public class ModuleApplyStepEventArgs : EventArgs
{
    public required ComfyNodeBuilder Builder { get; init; }

    public NodeDictionary Nodes => Builder.Nodes;

    public ModuleApplyStepTemporaryArgs Temp { get; } = new();

    /// <summary>
    /// Generation overrides (like hires fix generate, current seed generate, etc.)
    /// </summary>
    public IReadOnlyDictionary<Type, bool> IsEnabledOverrides { get; init; } = new Dictionary<Type, bool>();

    public List<(string SourcePath, string DestinationRelativePath)> FilesToTransfer { get; init; } = [];

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

    public class ModuleApplyStepTemporaryArgs
    {
        /// <summary>
        /// Temporary conditioning apply step, used by samplers to apply control net.
        /// </summary>
        public ConditioningConnections? Conditioning { get; set; }

        /// <summary>
        /// Temporary refiner conditioning apply step, used by samplers to apply control net.
        /// </summary>
        public ConditioningConnections? RefinerConditioning { get; set; }

        /// <summary>
        /// Temporary model apply step, used by samplers to apply control net.
        /// </summary>
        public ModelNodeConnection? Model { get; set; }
    }
}
