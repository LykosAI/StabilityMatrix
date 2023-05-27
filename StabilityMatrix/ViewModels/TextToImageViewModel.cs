using System;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Api;
using StabilityMatrix.Models.Api;

namespace StabilityMatrix.ViewModels;

public partial class TextToImageViewModel : ObservableObject
{
    private readonly IA3WebApi a3WebApi;

    [ObservableProperty]
    private string positivePromptText;
    
    [ObservableProperty]
    private string negativePromptText;
    
    [ObservableProperty]
    private int generationSteps;
    
    [ObservableProperty]
    private BitmapImage? imagePreview;
    
    public TextToImageViewModel(IA3WebApi a3WebApi)
    {
        this.a3WebApi = a3WebApi;
        positivePromptText = "Positive";
        negativePromptText = "Negative";
        generationSteps = 10;
    }
    
    private static BitmapImage Base64ToBitmap(string base64String)
    {
        var imageBytes = Convert.FromBase64String(base64String);
        
        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        
        using var stream = new MemoryStream(imageBytes, 0, imageBytes.Length);
        bitmapImage.StreamSource = stream;
        bitmapImage.EndInit();
        return bitmapImage;
    }

    [RelayCommand]
    private async void TextToImageGenerate()
    {
        var request = new TextToImageRequest
        {
            Prompt = PositivePromptText,
            NegativePrompt = NegativePromptText,
            Steps = GenerationSteps,
        };
        var response = await a3WebApi.TextToImage(request);
        // Decode base64 image
        var result = response.Images[0];
        var bitmap = Base64ToBitmap(result);

        ImagePreview = bitmap;
    }

}
