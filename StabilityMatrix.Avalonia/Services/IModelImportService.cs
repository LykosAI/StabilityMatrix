using System;
using System.Threading.Tasks;
using StabilityMatrix.Core.Models.Api;
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
        IProgress<ProgressReport>? progress = null,
        Func<Task>? onImportComplete = null,
        Func<Task>? onImportCanceled = null,
        Func<Task>? onImportFailed = null
    );
}
