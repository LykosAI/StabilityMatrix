using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NLog;
using StabilityMatrix.Api;
using StabilityMatrix.Helper;
using StabilityMatrix.Models.Api;
using ILogger = NLog.ILogger;

namespace StabilityMatrix.ViewModels;

public partial class TextToImageViewModel : ObservableObject
{
    private readonly ILogger<TextToImageViewModel> logger;
    private readonly IA3WebApi a3WebApi;
    private AsyncDispatcherTimer? progressQueryTimer;

    [ObservableProperty]
    private bool isGenerating;
    
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
    
    public Visibility ProgressBarVisibility => ProgressValue > 0 ? Visibility.Visible : Visibility.Collapsed;
    
    public TextToImageViewModel(IA3WebApi a3WebApi, ILogger<TextToImageViewModel> logger)
    {
        this.logger = logger;
        this.a3WebApi = a3WebApi;
        positivePromptText = "Positive";
        negativePromptText = "Negative";
        generationSteps = 10;
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
        var response = await a3WebApi.GetProgress(request);
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
        var task = a3WebApi.TextToImage(request);
        
        // Progress track while waiting for response
        StartProgressTracking();
        var response = await task;
        StopProgressTracking();

        // Decode base64 image
        var result = response.Images[0];
        var bitmap = Base64ToBitmap(result);

        ImagePreview = bitmap;
    }

}
