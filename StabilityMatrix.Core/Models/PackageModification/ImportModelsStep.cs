using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.PackageModification;

public class ImportModelsStep(
    ModelFinder modelFinder,
    IDownloadService downloadService,
    IModelIndexService modelIndexService,
    IEnumerable<string> files,
    DirectoryPath destinationFolder,
    bool isImportAsConnectedEnabled,
    bool moveFiles = false
) : IPackageStep
{
    public async Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        var copyPaths = files.ToDictionary(k => k, v => Path.Combine(destinationFolder, Path.GetFileName(v)));

        // remove files that are already in the folder
        foreach (var (source, destination) in copyPaths)
        {
            if (source == destination)
            {
                copyPaths.Remove(source);
            }
        }

        if (copyPaths.Count == 0)
        {
            progress?.Report(new ProgressReport(1f, message: "Import Complete"));
            return;
        }

        progress?.Report(new ProgressReport(0f, message: "Importing..."));

        var lastMessage = string.Empty;
        var transferProgress = new Progress<ProgressReport>(report =>
        {
            var message =
                copyPaths.Count > 1
                    ? $"Importing {report.Title} ({report.Message})"
                    : $"Importing {report.Title}";
            progress?.Report(
                new ProgressReport(
                    report.Progress ?? 0,
                    message: message,
                    printToConsole: message != lastMessage
                )
            );
            lastMessage = message;
        });

        if (moveFiles)
        {
            var filesMoved = 0;
            foreach (var (source, destination) in copyPaths)
            {
                try
                {
                    await FileTransfers.MoveFileAsync(source, destination).ConfigureAwait(false);
                    filesMoved++;
                }
                catch (Exception)
                {
                    // ignored
                }

                progress?.Report(
                    new ProgressReport(
                        filesMoved,
                        copyPaths.Count,
                        Path.GetFileName(source),
                        $"{filesMoved}/{copyPaths.Count}"
                    )
                );
            }
        }
        else
        {
            await FileTransfers.CopyFiles(copyPaths, transferProgress).ConfigureAwait(false);
        }

        // Hash files and convert them to connected model if found
        if (isImportAsConnectedEnabled)
        {
            var modelFilesCount = copyPaths.Count;
            var modelFiles = copyPaths.Values.Select(path => new FilePath(path));

            // Holds tasks for model queries after hash
            var modelQueryTasks = new List<Task<bool>>();

            foreach (var (i, modelFile) in modelFiles.Enumerate())
            {
                var hashProgress = new Progress<ProgressReport>(report =>
                {
                    var message =
                        modelFilesCount > 1
                            ? $"Computing metadata for {modelFile.Name} ({i}/{modelFilesCount})"
                            : $"Computing metadata for {modelFile.Name}";

                    progress?.Report(
                        new ProgressReport(
                            report.Progress ?? 0,
                            message: message,
                            printToConsole: message != lastMessage
                        )
                    );
                    lastMessage = message;
                });

                var hashBlake3 = await FileHash.GetBlake3Async(modelFile, hashProgress).ConfigureAwait(false);

                // Start a task to query the model in background
                var queryTask = Task.Run(async () =>
                {
                    var result = await modelFinder.LocalFindModel(hashBlake3).ConfigureAwait(false);
                    result ??= await modelFinder.RemoteFindModel(hashBlake3).ConfigureAwait(false);

                    if (result is null)
                        return false; // Not found

                    var (model, version, file) = result.Value;

                    // Save connected model info json
                    var modelFileName = Path.GetFileNameWithoutExtension(modelFile.Info.Name);
                    var modelInfo = new ConnectedModelInfo(model, version, file, DateTimeOffset.UtcNow);
                    await modelInfo
                        .SaveJsonToDirectory(destinationFolder, modelFileName)
                        .ConfigureAwait(false);

                    // If available, save thumbnail
                    var image = version.Images?.FirstOrDefault(x => x.Type == "image");
                    if (image != null)
                    {
                        var imageExt = Path.GetExtension(image.Url).TrimStart('.');
                        if (imageExt is "jpg" or "jpeg" or "png")
                        {
                            var imageDownloadPath = Path.GetFullPath(
                                Path.Combine(destinationFolder, $"{modelFileName}.preview.{imageExt}")
                            );
                            await downloadService
                                .DownloadToFileAsync(image.Url, imageDownloadPath)
                                .ConfigureAwait(false);
                        }
                    }

                    return true;
                });
                modelQueryTasks.Add(queryTask);
            }

            // Set progress to indeterminate
            progress?.Report(
                new ProgressReport
                {
                    IsIndeterminate = true,
                    Progress = -1f,
                    Message = "Checking connected model information"
                }
            );

            // Wait for all model queries to finish
            var modelQueryResults = await Task.WhenAll(modelQueryTasks).ConfigureAwait(false);

            var successCount = modelQueryResults.Count(r => r);
            var totalCount = modelQueryResults.Length;
            var failCount = totalCount - successCount;

            var progressText = successCount switch
            {
                0 when failCount > 0 => "Import complete. No connected data found.",
                > 0 when failCount > 0
                    => $"Import complete. Found connected data for {successCount} of {totalCount} models.",
                1 when failCount == 0 => "Import complete. Found connected data for 1 model.",
                _ => $"Import complete. Found connected data for all {totalCount} models."
            };

            progress?.Report(new ProgressReport(1f, message: progressText));
        }
        else
        {
            progress?.Report(new ProgressReport(1f, message: "Import Complete"));
        }

        await modelIndexService.RefreshIndex().ConfigureAwait(false);
    }

    public string ProgressTitle => "Importing Models";
}
