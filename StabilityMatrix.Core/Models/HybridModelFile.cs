using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Core.Models;

/// <summary>
/// Model file union that may be remote or local.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public partial record HybridModelFile
{
    /// <summary>
    /// Singleton instance of <see cref="HybridModelFile"/> that represents use of a default model.
    /// </summary>
    public static HybridModelFile Default => new();
    
    /// <summary>
    /// Singleton instance of <see cref="HybridModelFile"/> that represents no model.
    /// </summary>
    public static HybridModelFile None => new();

    private string? RemoteName { get; init; }
    
    public LocalModelFile? Local { get; init; }

    [MemberNotNullWhen(true, nameof(RemoteName))]
    [MemberNotNullWhen(false, nameof(Local))]
    [JsonIgnore]
    public bool IsRemote => RemoteName != null;
    
    [JsonIgnore]
    public string FileName => IsRemote 
        ? RemoteName : Local.FileName;

    [JsonIgnore]
    public string ShortDisplayName => Path.GetFileNameWithoutExtension(FileName);
    
    public static HybridModelFile FromLocal(LocalModelFile local)
    {
        return new HybridModelFile
        {
            Local = local
        };
    }
    
    public static HybridModelFile FromRemote(string remoteName)
    {
        return new HybridModelFile
        {
            RemoteName = remoteName
        };
    }
}
