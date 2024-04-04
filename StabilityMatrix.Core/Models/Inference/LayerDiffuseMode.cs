using System.ComponentModel.DataAnnotations;

namespace StabilityMatrix.Core.Models.Inference;

public enum LayerDiffuseMode
{
    /// <summary>
    /// The layer diffuse mode is not set.
    /// </summary>
    [Display(Name = "None")]
    None,

    /// <summary>
    /// Generate foreground only with transparency.
    /// </summary>
    [Display(Name = "Generate Foreground with Transparency")]
    GenerateForegroundWithTransparency,
}
