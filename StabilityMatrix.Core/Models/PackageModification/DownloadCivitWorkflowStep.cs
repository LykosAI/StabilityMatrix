using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Api.OpenArt;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.PackageModification;

/// <summary>
/// Downloads a CivitAI workflow file (a zip of ComfyUI workflow jsons, or a bare json) and
/// imports the contained workflows into the shared workflow library, embedding
/// <see cref="WorkflowMetadata"/> in each file for the installed workflows page.
/// </summary>
public class DownloadCivitWorkflowStep(
    CivitModel model,
    CivitModelVersion version,
    CivitFile file,
    IDownloadService downloadService,
    ISettingsManager settingsManager
) : IPackageStep
{
    public string ProgressTitle => "Downloading Workflow";

    public async Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        var tempFile = new FilePath(
            Path.GetTempPath(),
            $"sm-workflow-{Guid.NewGuid():N}{Path.GetExtension(file.Name)}"
        );

        try
        {
            await downloadService
                .DownloadToFileAsync(file.GetFileSpecificDownloadUrl(), tempFile, progress)
                .ConfigureAwait(false);

            var targetDir = settingsManager.WorkflowDirectory.JoinDir(SanitizeFileName(model.Name));
            targetDir.Create();

            var importedCount =
                tempFile.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)
                    ? await ExtractArchiveAsync(tempFile, targetDir).ConfigureAwait(false)
                : await ImportWorkflowJsonAsync(
                        await File.ReadAllTextAsync(tempFile).ConfigureAwait(false),
                        Path.GetFileNameWithoutExtension(file.Name),
                        targetDir,
                        isMultiple: false
                    )
                    .ConfigureAwait(false)
                    ? 1
                : 0;

            if (importedCount == 0)
            {
                throw new InvalidOperationException(
                    $"No ComfyUI workflow json found in \"{file.Name}\" - "
                        + "the file may not contain importable workflows"
                );
            }

            progress?.Report(new ProgressReport(1f, $"Imported {importedCount} workflow(s)"));
        }
        finally
        {
            if (tempFile.Exists)
            {
                tempFile.Delete();
            }
        }
    }

    private async Task<int> ExtractArchiveAsync(FilePath archivePath, DirectoryPath targetDir)
    {
        using var archive = ZipFile.OpenRead(archivePath);

        var entries = archive
            .Entries.Where(entry =>
                !string.IsNullOrEmpty(entry.Name)
                && entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                && !entry.FullName.Contains("__MACOSX", StringComparison.OrdinalIgnoreCase)
            )
            .ToList();

        var importedCount = 0;
        foreach (var entry in entries)
        {
            using var reader = new StreamReader(entry.Open());
            var json = await reader.ReadToEndAsync().ConfigureAwait(false);

            if (
                await ImportWorkflowJsonAsync(
                        json,
                        Path.GetFileNameWithoutExtension(entry.Name),
                        targetDir,
                        isMultiple: entries.Count > 1
                    )
                    .ConfigureAwait(false)
            )
            {
                importedCount++;
            }
        }

        return importedCount;
    }

    /// <summary>
    /// Writes a single workflow json into the target directory with metadata embedded.
    /// Returns false when the content is not a json object and was skipped.
    /// </summary>
    private async Task<bool> ImportWorkflowJsonAsync(
        string json,
        string name,
        DirectoryPath targetDir,
        bool isMultiple
    )
    {
        JsonObject? workflow;
        try
        {
            workflow = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return false;
        }

        if (workflow is null)
            return false;

        var metadata = JsonSerializer.SerializeToNode(CreateMetadata(name, isMultiple))!.AsObject();
        foreach (var (key, value) in metadata.ToList())
        {
            metadata.Remove(key);
            workflow[key] = value;
        }

        var filePath = targetDir.JoinFile($"{SanitizeFileName(name)}.json");
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(workflow)).ConfigureAwait(false);

        return true;
    }

    private WorkflowMetadata CreateMetadata(string name, bool isMultiple)
    {
        var thumbnail = (version.Images ?? model.ModelVersions?.FirstOrDefault()?.Images)?.FirstOrDefault(
            image => image.Type == "image"
        );

        return new WorkflowMetadata
        {
            SourceUrl = $"https://civitai.com/models/{model.Id}?modelVersionId={version.Id}",
            Workflow = new OpenArtSearchResult
            {
                // Unique per json so the installed workflows cache never collides
                Id = $"civitai-{model.Id}-{version.Id}-{name}",
                Name = isMultiple ? $"{model.Name} ({name})" : model.Name,
                Creator = model.Creator is { } creator
                    ? new OpenArtCreator
                    {
                        Name = creator.Username ?? string.Empty,
                        Username = creator.Username ?? string.Empty,
                        Avatar = Uri.TryCreate(creator.Image, UriKind.Absolute, out var avatar)
                            ? avatar
                            : null,
                        DevProfileUrl = creator.ProfileUrl ?? string.Empty,
                    }
                    : null,
                Stats = new OpenArtStats
                {
                    NumDownloads = model.Stats?.DownloadCount ?? 0,
                    NumLikes = model.Stats?.ThumbsUpCount ?? 0,
                    NumReviews = model.Stats?.RatingCount ?? 0,
                    Rating = model.Stats?.Rating ?? 0,
                },
                Thumbnails = thumbnail is null
                    ? []
                    :
                    [
                        new OpenArtThumbnail
                        {
                            Url = new Uri(thumbnail.Url),
                            Width = thumbnail.Width,
                            Height = thumbnail.Height,
                        },
                    ],
            },
        };
    }

    private static string SanitizeFileName(string name) =>
        Path.GetInvalidFileNameChars().Aggregate(name, (current, c) => current.Replace(c, '_')).Trim();
}
