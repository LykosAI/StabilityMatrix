using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

/// <summary>
/// Edits the display metadata of an installed workflow: name and description are written
/// into the file's embedded metadata; a chosen picture is saved as a
/// <c>{name}.preview.{ext}</c> sidecar next to the workflow file.
/// </summary>
[View(typeof(WorkflowMetadataEditorDialog))]
[ManagedService]
[RegisterTransient<WorkflowMetadataEditorViewModel>]
public partial class WorkflowMetadataEditorViewModel : ContentDialogViewModelBase
{
    private static readonly string[] SupportedImageExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    public required InstalledWorkflow Workflow { get; init; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    /// <summary>Image shown in the dialog: a newly chosen file, or the current thumbnail.</summary>
    [ObservableProperty]
    public partial string? PreviewImageSource { get; set; }

    private string? selectedImagePath;

    public override void OnLoaded()
    {
        base.OnLoaded();

        Name = Workflow.Workflow?.Name ?? string.Empty;
        Description = Workflow.Workflow?.Description ?? string.Empty;
        PreviewImageSource = Workflow.DisplayThumbnail;
    }

    [RelayCommand]
    private async Task ChooseImageAsync()
    {
        var files = await App.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Images")
                    {
                        Patterns = SupportedImageExtensions.Select(ext => $"*{ext}").ToList(),
                    },
                ],
            }
        );

        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
        {
            selectedImagePath = path;
            PreviewImageSource = path;
        }
    }

    /// <summary>
    /// Writes the edited metadata into the workflow file and copies a chosen image
    /// to its preview sidecar. Call after the dialog closes with the primary result.
    /// </summary>
    public async Task SaveAsync()
    {
        if (Workflow.FilePath?.FirstOrDefault()?.TryGetLocalPath() is not { } workflowPath)
            return;

        if (JsonNode.Parse(await File.ReadAllTextAsync(workflowPath)) is not JsonObject root)
            return;

        // Plain files without metadata gain a minimal local identity on first edit
        var metadata = root["sm_workflow_data"] as JsonObject ?? new JsonObject();
        metadata["id"] ??= $"local-{Guid.NewGuid():N}";
        metadata["name"] = Name;
        metadata["description"] = Description;

        root["sm_workflow_data"] = metadata;
        await File.WriteAllTextAsync(workflowPath, JsonSerializer.Serialize(root));

        if (selectedImagePath is not null)
        {
            var workflowFile = new FilePath(workflowPath);
            var sidecarBaseName = $"{workflowFile.NameWithoutExtension}.preview";

            foreach (var oldSidecar in workflowFile.Directory!.EnumerateFiles($"{sidecarBaseName}.*"))
            {
                oldSidecar.Delete();
            }

            var extension = Path.GetExtension(selectedImagePath).ToLowerInvariant();
            File.Copy(
                selectedImagePath,
                workflowFile.Directory!.JoinFile($"{sidecarBaseName}{extension}"),
                overwrite: true
            );
        }
    }
}
