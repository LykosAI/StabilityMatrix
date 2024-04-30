using System.ComponentModel.DataAnnotations;

namespace StabilityMatrix.Core.Models.Settings;

public enum NumberFormatMode
{
    /// <summary>
    /// Use the default number format
    /// </summary>
    [Display(Name = "Default")]
    Default,

    /// <summary>
    /// Use the number format from the current culture
    /// </summary>
    [Display(Name = "Locale Specific")]
    CurrentCulture,

    /// <summary>
    /// Use the number format from the invariant culture
    /// </summary>
    [Display(Name = "Invariant")]
    InvariantCulture
}
