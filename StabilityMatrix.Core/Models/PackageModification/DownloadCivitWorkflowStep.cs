using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Api.OpenArt;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.PackageModification;

/// <summary>
/// Downloads a CivitAI workflow file - an archive or bare file of workflow jsons and/or
/// workflow-embedded images - and imports the contained workflows into the shared workflow
/// library, embedding <see cref="WorkflowMetadata"/> in each file for the installed
/// workflows page.
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

    /// <summary>
    /// Library paths of the workflow files this step imported, populated during execution.
    /// </summary>
    public List<FilePath> ImportedFiles { get; } = [];

    private bool foundGenerationParametersOnly;

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

            var fileStem = Path.GetFileNameWithoutExtension(file.Name);

            var importedCount = tempFile.Extension.ToLowerInvariant() switch
            {
                ".zip" => await ExtractArchiveAsync(tempFile, targetDir).ConfigureAwait(false),
                ".png" => await ImportWorkflowFromPngAsync(
                        await File.ReadAllBytesAsync(tempFile).ConfigureAwait(false),
                        fileStem,
                        targetDir,
                        isMultiple: false
                    )
                    .ConfigureAwait(false)
                    ? 1
                    : 0,
                _ => await ImportWorkflowJsonAsync(
                        await File.ReadAllTextAsync(tempFile).ConfigureAwait(false),
                        fileStem,
                        targetDir,
                        isMultiple: false
                    )
                    .ConfigureAwait(false)
                    ? 1
                    : 0,
            };

            if (importedCount == 0)
            {
                throw new InvalidOperationException(
                    foundGenerationParametersOnly
                        ? $"\"{file.Name}\" contains images with WebUI-style generation parameters, "
                            + "but no embedded ComfyUI workflow - the creator may not have attached "
                            + "the actual workflow to this file"
                        : $"No ComfyUI workflows found in \"{file.Name}\" - "
                            + "the file contains neither workflow json nor images with an embedded workflow"
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
                && !entry.FullName.Contains("__MACOSX", StringComparison.OrdinalIgnoreCase)
                && (
                    entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    || entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                )
            )
            .ToList();

        var importedCount = 0;
        foreach (var entry in entries)
        {
            var entryStem = Path.GetFileNameWithoutExtension(entry.Name);
            bool imported;

            if (entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                using var memoryStream = new MemoryStream();
                await using (var entryStream = entry.Open())
                {
                    await entryStream.CopyToAsync(memoryStream).ConfigureAwait(false);
                }

                imported = await ImportWorkflowFromPngAsync(
                        memoryStream.ToArray(),
                        entryStem,
                        targetDir,
                        isMultiple: entries.Count > 1
                    )
                    .ConfigureAwait(false);
            }
            else
            {
                using var reader = new StreamReader(entry.Open());
                var json = await reader.ReadToEndAsync().ConfigureAwait(false);

                imported = await ImportWorkflowJsonAsync(
                        json,
                        entryStem,
                        targetDir,
                        isMultiple: entries.Count > 1
                    )
                    .ConfigureAwait(false);
            }

            if (imported)
            {
                importedCount++;
            }
        }

        return importedCount;
    }

    /// <summary>
    /// Imports a workflow from the "workflow" metadata chunk ComfyUI embeds in saved images
    /// (the same data loaded when dragging the image into ComfyUI). The image itself is kept
    /// as the imported workflow's preview. Returns false when no workflow is embedded.
    /// </summary>
    private async Task<bool> ImportWorkflowFromPngAsync(
        byte[] pngBytes,
        string name,
        DirectoryPath targetDir,
        bool isMultiple
    )
    {
        string workflowJson;
        try
        {
            using var reader = new BinaryReader(new MemoryStream(pngBytes));
            workflowJson = ImageMetadata.ReadTextChunk(reader, "workflow");

            if (string.IsNullOrEmpty(workflowJson))
            {
                // Distinguish A1111/Forge-style showcase renders for a clearer error when
                // nothing in the download turns out to be importable
                if (!string.IsNullOrEmpty(ImageMetadata.ReadTextChunk(reader, "parameters")))
                {
                    foundGenerationParametersOnly = true;
                }

                return false;
            }
        }
        catch (Exception)
        {
            return false;
        }

        if (!await ImportWorkflowJsonAsync(workflowJson, name, targetDir, isMultiple).ConfigureAwait(false))
            return false;

        // Workflow screenshots can be huge full-graph captures; store a bounded preview
        await File.WriteAllBytesAsync(
                targetDir.JoinFile($"{SanitizeFileName(name)}.preview.png"),
                ImageThumbnailHelper.CreateThumbnail(pngBytes)
            )
            .ConfigureAwait(false);

        return true;
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

        ImportedFiles.Add(filePath);
        return true;
    }

    private WorkflowMetadata CreateMetadata(string name, bool isMultiple)
    {
        var thumbnail = (version.Images ?? model.ModelVersions?.FirstOrDefault()?.Images)?.FirstOrDefault(
            image => image.Type == "image"
        );

        // Full-size workflow screenshots are heavy; cards never need more than this
        var thumbnailUrl = thumbnail is null ? null : CivitaiUrlHelper.CapImageWidth(thumbnail.Url, 700);

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
                Thumbnails = thumbnailUrl is null
                    ? []
                    :
                    [
                        new OpenArtThumbnail
                        {
                            Url = new Uri(thumbnailUrl),
                            Width = thumbnail!.Width,
                            Height = thumbnail.Height,
                        },
                    ],
            },
        };
    }

    private static string SanitizeFileName(string name) =>
        Path.GetInvalidFileNameChars().Aggregate(name, (current, c) => current.Replace(c, '_')).Trim();
}
