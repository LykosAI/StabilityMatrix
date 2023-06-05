using System;
using System.Diagnostics;
using System.IO;
using System.Net.Mime;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Models;
using StabilityMatrix.Models.Api;
using StabilityMatrix.Services;

namespace StabilityMatrix.ViewModels;

public partial class CheckpointBrowserCardViewModel : ProgressViewModel
{
    private readonly IDownloadService downloadService;
    public CivitModel CivitModel { get; init; }

    public override Visibility ProgressVisibility => Value > 0 ? Visibility.Visible : Visibility.Collapsed;
    public override Visibility TextVisibility => Value > 0 ? Visibility.Visible : Visibility.Collapsed;

    public CheckpointBrowserCardViewModel(CivitModel civitModel, IDownloadService downloadService)
    {
        this.downloadService = downloadService;
        CivitModel = civitModel;
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
        Text = "Downloading...";

        var latestModelFile = model.ModelVersions[0].Files[0];
        
        var downloadPath = Path.Combine(SharedFolders.SharedFoldersPath,
            SharedFolders.SharedFolderTypeToName(model.Type.ToSharedFolderType()), latestModelFile.Name);

        var progress = new Progress<ProgressReport>(progress =>
        {
            Value = progress.Percentage;
            Text = $"Importing... {progress.Percentage}%";
        });
        await downloadService.DownloadToFileAsync(latestModelFile.DownloadUrl, downloadPath, progress: progress);
        
        Text = "Import complete!";
        Value = 100;
    }
}
