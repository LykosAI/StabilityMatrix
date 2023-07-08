using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;
using StabilityMatrix.Services;

namespace StabilityMatrix.ViewModels;

public partial class TextToImageViewModel : ObservableObject
{
    private readonly ILogger<TextToImageViewModel> logger;
    private readonly IA3WebApiManager a3WebApiManager;
    private readonly ISnackbarService snackbarService;
    private readonly PageContentDialogService pageContentDialogService;
    private readonly ISettingsManager settingsManager;
    private AsyncDispatcherTimer? progressQueryTimer;

    [ObservableProperty]
    private bool isGenerating;

    [ObservableProperty]
    private bool connectionFailed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressRingVisibility))]
    [NotifyPropertyChangedFor(nameof(ImagePreviewVisibility))]
    private bool isProgressRingActive;
    
    public Visibility ProgressRingVisibility => IsProgressRingActive ? Visibility.Visible : Visibility.Collapsed;
    
    public Visibility ImagePreviewVisibility => IsProgressRingActive ? Visibility.Collapsed : Visibility.Visible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressBarVisibility))]
    private int progressValue;

    [ObservableProperty]
    private string positivePromptText;
    
    [ObservableProperty]
    private string negativePromptText;
    
    [ObservableProperty]
    private int generationSteps;
    
    [ObservableProperty]
    private BitmapImage? imagePreview;

    [ObservableProperty]
    private CheckpointFolder? diffusionCheckpointFolder;
    
    [ObservableProperty]
    private CheckpointFile? selectedCheckpointFile;
    
    public Visibility ProgressBarVisibility => ProgressValue > 0 ? Visibility.Visible : Visibility.Collapsed;
    
    public List<string> Samplers { get; } = new()
    {
        "Euler a", 
        "Euler", 
        "DPM++ 2M Karras"
    };

    public TextToImageViewModel(IA3WebApiManager a3WebApiManager, ILogger<TextToImageViewModel> logger, ISnackbarService snackbarService, PageContentDialogService pageContentDialogService, ISettingsManager settingsManager)
    {
        this.logger = logger;
        this.a3WebApiManager = a3WebApiManager;
        this.snackbarService = snackbarService;
        this.pageContentDialogService = pageContentDialogService;
        this.settingsManager = settingsManager;
        positivePromptText = "Positive";
        negativePromptText = "Negative";
        generationSteps = 10;
    }
    
    public async Task OnLoaded()
    {
        if (ConnectionFailed)
        {
            await PromptRetryConnection();
        }
        else
        {
            await CheckConnection();
        }
        
        // Set the diffusion checkpoint folder
        var sdModelsDir = Path.Join(settingsManager.ModelsDirectory, SharedFolderType.StableDiffusion.GetStringValue());
        if (!Directory.Exists(sdModelsDir))
        {
            logger.LogWarning("Skipped model folder index - {SdModelsDir} does not exist", sdModelsDir);
            return;
        }
        DiffusionCheckpointFolder = new CheckpointFolder(null!, null!, null!, null!) // TODO: refactor to not use view models
        {
            Title = Path.GetFileName(sdModelsDir),
            DirectoryPath = sdModelsDir
        };
        // Index the folder
        await DiffusionCheckpointFolder.IndexAsync();
        // Set the active model from the api
        await SetActiveModelFromApi();
    }

    private async Task SetActiveModelFromApi()
    {
        var task = a3WebApiManager.Client.GetOptions();
        var responseResult = await snackbarService.TryAsync(task, "Failed to get options");
        if (responseResult is {IsSuccessful: true, Result: not null})
        {
            // Find file
            var options = responseResult.Result;
            var checkpointFile = DiffusionCheckpointFolder?
                .CheckpointFiles.FirstOrDefault(f => f.FileName == options.SdModelCheckpoint);
            logger.LogInformation("Set active checkpoint from api {CheckpointFile}", checkpointFile?.FileName);
            SelectedCheckpointFile = checkpointFile;
        }
    }

    // Checks connection, if unsuccessful, shows a content dialog to retry
    private async Task CheckConnection()
    {
        try
        { 
            await a3WebApiManager.Client.GetPing();
            ConnectionFailed = false;
        }
        catch (Exception e)
        {
            // On error, show a content dialog to retry
            ConnectionFailed = true;
            logger.LogWarning("Ping response failed: {EMessage}", e.Message);
            var dialog = pageContentDialogService.CreateDialog();
            dialog.Title = "Connection failed";
            dialog.Content = "Please check the server is running with the --api launch option enabled.";
            dialog.CloseButtonText = "Retry";
            dialog.IsPrimaryButtonEnabled = false;
            dialog.IsSecondaryButtonEnabled = false;
            await dialog.ShowAsync();
            // Retry
            await CheckConnection();
        }
    }

    private async Task PromptRetryConnection()
    {
        var dialog = pageContentDialogService.CreateDialog();
        dialog.Title = "Connection failed";
        dialog.Content = "Please check the server is running with the --api launch option enabled.";
        dialog.CloseButtonText = "Retry";
        dialog.IsPrimaryButtonEnabled = false;
        dialog.IsSecondaryButtonEnabled = false;
        await dialog.ShowAsync();
        // Retry
        await CheckConnection();
    }

    private void StartProgressTracking(TimeSpan? interval = null)
    {
        progressQueryTimer = new AsyncDispatcherTimer
        {
            Interval = interval ?? TimeSpan.FromMilliseconds(150),
            IsReentrant = false,
            TickTask = OnProgressTrackingTick,
        };
        progressQueryTimer.Start();
    }
    
    private void StopProgressTracking()
    {
        IsProgressRingActive = false;
        ProgressValue = 0;
        progressQueryTimer?.Stop();
    }
    
    private async Task OnProgressTrackingTick()
    {
        var request = new ProgressRequest();
        var task = a3WebApiManager.Client.GetProgress(request);
        var responseResult = await snackbarService.TryAsync(task, "Failed to get progress");
        if (!responseResult.IsSuccessful || responseResult.Result == null)
        {
            StopProgressTracking();
            return;
        }

        var response = responseResult.Result;
        var progress = response.Progress;
        logger.LogInformation("Image Progress: {ResponseProgress}, ETA: {ResponseEtaRelative} s", response.Progress, response.EtaRelative);
        if (Math.Abs(progress - 1.0) < 0.01)
        {
            ProgressValue = 100;
            progressQueryTimer?.Stop();
        }
        else
        {
            // Update progress
            ProgressValue = (int) Math.Clamp(Math.Ceiling(progress * 100), 0, 100);
            // Update preview image
            var result = response.CurrentImage;
            if (result != null)
            {
                // Stop indeterminate progress ring
                IsProgressRingActive = false;
                // Set preview image
                var bitmap = Base64ToBitmap(result);
                ImagePreview = bitmap;
            }
        }
    }
    
    private static BitmapImage Base64ToBitmap(string base64String)
    {
        var imageBytes = Convert.FromBase64String(base64String);
        
        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        
        using var ms = new MemoryStream(imageBytes, 0, imageBytes.Length);
        bitmapImage.StreamSource = ms;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        return bitmapImage;
    }

    [RelayCommand]
    private async void TextToImageGenerate()
    {
        // Start indeterminate progress ring
        IsProgressRingActive = true;
        
        var request = new TextToImageRequest
        {
            Prompt = PositivePromptText,
            NegativePrompt = NegativePromptText,
            Steps = GenerationSteps,
        };
        var task = a3WebApiManager.Client.TextToImage(request);
        
        // Progress track while waiting for response
        StartProgressTracking();
        var response = await snackbarService.TryAsync(task, "Failed to get a response from the server");
        StopProgressTracking();

        if (!response.IsSuccessful || response.Result == null) return;
        
        // Decode base64 image
        var result = response.Result.Images[0];
        var bitmap = Base64ToBitmap(result);

        ImagePreview = bitmap;
    }

}
