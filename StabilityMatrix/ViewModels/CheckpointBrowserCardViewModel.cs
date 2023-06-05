using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Models;
using StabilityMatrix.Models.Api;

namespace StabilityMatrix.ViewModels;

public partial class CheckpointBrowserCardViewModel : ObservableObject
{
    public CivitModel CivitModel { get; init; }
    
    [ObservableProperty] private int importProgress;
    [ObservableProperty] private string importStatus;
    
    public CheckpointBrowserCardViewModel(CivitModel civitModel)
    {
        CivitModel = civitModel;
    }
    private void DownloadServiceOnDownloadComplete(object? sender, ProgressReport e)
    {
        ImportStatus = "Import complete!";
        ImportProgress = 100;
    }

    private void DownloadServiceOnDownloadProgressChanged(object? sender, ProgressReport e)
    {
        ImportProgress = (int)e.Percentage;
        ImportStatus = $"Importing... {e.Percentage}%";
    }

    [RelayCommand]
    private void OpenModel()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = $"https://civitai.com/models/{CivitModel.Id}",
            UseShellExecute = true
        });
    }
    
    [RelayCommand]
    private async Task Import(CivitModel model)
    {
        IsIndeterminate = false;
        ImportStatus = "Downloading...";

        var latestModelFile = model.ModelVersions[0].Files[0];
        
        var downloadPath = Path.Combine(SharedFolders.SharedFoldersPath,
            SharedFolders.SharedFolderTypeToName(model.Type.ToSharedFolderType()), latestModelFile.Name);

        downloadService.DownloadProgressChanged += DownloadServiceOnDownloadProgressChanged;
        downloadService.DownloadComplete += DownloadServiceOnDownloadComplete;
        
        await downloadService.DownloadToFileAsync(latestModelFile.DownloadUrl, downloadPath);
        
        downloadService.DownloadProgressChanged -= DownloadServiceOnDownloadProgressChanged;
        downloadService.DownloadComplete -= DownloadServiceOnDownloadComplete;
    }
}
