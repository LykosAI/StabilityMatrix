namespace StabilityMatrix.Avalonia.Models;

/// <summary>
/// How a model import reacts when the file's hash matches an already-installed model.
/// </summary>
public enum DuplicateDownloadHandling
{
    /// <summary>Ask the user whether to download anyway or skip.</summary>
    Prompt,

    /// <summary>Skip silently with a notification. Suited to bulk downloads.</summary>
    Skip,

    /// <summary>Download without checking for duplicates.</summary>
    Allow,
}
