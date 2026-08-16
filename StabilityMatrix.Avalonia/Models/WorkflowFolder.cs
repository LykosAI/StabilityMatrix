namespace StabilityMatrix.Avalonia.Models;

/// <summary>
/// A folder filter entry on the installed workflows page.
/// </summary>
/// <param name="Key">
/// Folder name to match against <see cref="InstalledWorkflow.Folder"/>;
/// empty string for library-root files, null for the "all workflows" entry.
/// </param>
/// <param name="DisplayName">Localized display name.</param>
public record WorkflowFolder(string? Key, string DisplayName);
