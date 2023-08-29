using System.ComponentModel;

namespace StabilityMatrix.Core.Models;

/// <summary>
/// Design time extensions for <see cref="HybridModelFile"/>.
/// </summary>
[DesignOnly(true)]
public partial record HybridModelFile
{
    /// <summary>
    /// Whether this instance is the default model.
    /// </summary>
    public bool IsDefault => ReferenceEquals(this, Default);
    
    /// <summary>
    /// Whether this instance is no model.
    /// </summary>
    public bool IsNone => ReferenceEquals(this, None);
}
