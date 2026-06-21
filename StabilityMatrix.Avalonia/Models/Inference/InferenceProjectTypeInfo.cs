using System.Collections.Generic;
using FluentIcons.Common;
using StabilityMatrix.Core.Models.Inference;

namespace StabilityMatrix.Avalonia.Models.Inference;

/// <summary>
/// Display metadata for an <see cref="InferenceProjectType"/>, used to build the
/// "new tab" gallery picker. Keeps the picker data-driven instead of hardcoded in XAML.
/// </summary>
public record InferenceProjectTypeInfo
{
    public required InferenceProjectType Type { get; init; }

    public required string DisplayName { get; init; }

    public required Symbol Icon { get; init; }

    public string? Description { get; init; }

    public required InferenceProjectCategory Category { get; init; }
}

/// <summary>
/// Grouping for inference project types in the new-tab picker.
/// </summary>
public enum InferenceProjectCategory
{
    /// <summary>Standard image workflows (text-to-image, image-to-image, upscale).</summary>
    Image,

    /// <summary>Video workflows.</summary>
    Video,

    /// <summary>
    /// Superseded or single-purpose workflows kept for back-compat with existing projects,
    /// tucked out of the way so they don't clutter the picker for new users.
    /// </summary>
    Legacy,
}

/// <summary>
/// A category header plus its project type options, for the grouped picker view.
/// </summary>
public record InferenceProjectTypeCategoryGroup
{
    public required string Header { get; init; }

    public required IReadOnlyList<InferenceProjectTypeInfo> Items { get; init; }
}
