using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Api.OpenModelsDb;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Avalonia.Services;

public interface IModelImportService
{
    /// <summary>
    /// Saves the preview image to the same directory as the model file
    /// </summary>
    /// <param name="modelVersion"></param>
    /// <param name="modelFilePath"></param>
    /// <returns>The file path of the saved preview image</returns>
    Task<FilePath?> SavePreviewImage(CivitModelVersion modelVersion, FilePath modelFilePath);

    Task DoImport(
        CivitModel model,
        DirectoryPath downloadFolder,
        CivitModelVersion? selectedVersion = null,
        CivitFile? selectedFile = null,
        string? fileNameOverride = null,
        SamplerCardViewModel? inferenceDefaults = null,
        IProgress<ProgressReport>? progress = null,
        Func<Task>? onImportComplete = null,
        Func<Task>? onImportCanceled = null,
        Func<Task>? onImportFailed = null
    );

    /// <summary>
    /// Imports a model from OpenModelsDb
    /// </summary>
    Task DoOpenModelDbImport(
        OpenModelDbKeyedModel model,
        OpenModelDbResource resource,
        DirectoryPath downloadFolder,
        Action<TrackedDownload>? configureDownload = null
    );

    /// <summary>
    /// Imports a model from a custom source
    /// </summary>
    /// <param name="modelUri">Url to download the model</param>
    /// <param name="modelFileName">Model file name with extension</param>
    /// <param name="downloadFolder">Local directory to save model file</param>
    /// <param name="previewImageUri">Url to download the preview image</param>
    /// <param name="previewImageFileExtension">Preview image file name with extension</param>
    /// <param name="connectedModelInfo">Connected model info</param>
    /// <param name="configureDownload">Optional configuration for the download</param>
    Task DoCustomImport(
        Uri modelUri,
        string modelFileName,
        DirectoryPath downloadFolder,
        Uri? previewImageUri = null,
        string? previewImageFileExtension = null,
        ConnectedModelInfo? connectedModelInfo = null,
        Action<TrackedDownload>? configureDownload = null
    ) =>
        DoCustomImport(
            [modelUri],
            modelFileName,
            downloadFolder,
            previewImageUri,
            previewImageFileExtension,
            connectedModelInfo,
            configureDownload
        );

    /// <summary>
    /// Imports a model from a custom source
    /// </summary>
    /// <param name="modelUris">Urls to download the model. If multiple provided each will be attempted in order.</param>
    /// <param name="modelFileName">Model file name with extension</param>
    /// <param name="downloadFolder">Local directory to save model file</param>
    /// <param name="previewImageUri">Url to download the preview image</param>
    /// <param name="previewImageFileExtension">Preview image file name with extension</param>
    /// <param name="connectedModelInfo">Connected model info</param>
    /// <param name="configureDownload">Optional configuration for the download</param>
    Task DoCustomImport(
        IEnumerable<Uri> modelUris,
        string modelFileName,
        DirectoryPath downloadFolder,
        Uri? previewImageUri = null,
        string? previewImageFileExtension = null,
        ConnectedModelInfo? connectedModelInfo = null,
        Action<TrackedDownload>? configureDownload = null
    );
}
