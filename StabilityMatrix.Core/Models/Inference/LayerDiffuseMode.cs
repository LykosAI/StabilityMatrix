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
    /// Generate foreground only with transparency. SD1.5
    /// </summary>
    [Display(Name = "(SD 1.5) Generate Foreground with Transparency")]
    GenerateForegroundWithTransparencySD15,

    /// <summary>
    /// Generate foreground only with transparency. SDXL
    /// </summary>
    [Display(Name = "(SDXL) Generate Foreground with Transparency")]
    GenerateForegroundWithTransparencySDXL,
}
