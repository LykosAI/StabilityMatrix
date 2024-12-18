using System;
using System.ComponentModel.DataAnnotations;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ControlNetCard))]
[ManagedService]
[RegisterTransient<ControlNetCardViewModel>]
public partial class ControlNetCardViewModel : LoadableViewModelBase
{
    public const string ModuleKey = "ControlNet";

    private readonly ServiceManager<ViewModelBase> vmFactory;

    [ObservableProperty]
    [Required]
    private HybridModelFile? selectedModel;

    [ObservableProperty]
    [Required]
    private ComfyAuxPreprocessor? selectedPreprocessor;

    [ObservableProperty]
    [Required]
    [Range(0, 2048)]
    private int width;

    [ObservableProperty]
    [Required]
    [Range(0, 2048)]
    private int height;

    [ObservableProperty]
    [Required]
    [Range(0d, 10d)]
    private double strength = 1.0;

    [ObservableProperty]
    [Required]
    [Range(0d, 1d)]
    private double startPercent;

    [ObservableProperty]
    [Required]
    [Range(0d, 1d)]
    private double endPercent = 1.0;

    public SelectImageCardViewModel SelectImageCardViewModel { get; }

    public IInferenceClientManager ClientManager { get; }

    public ControlNetCardViewModel(
        IInferenceClientManager clientManager,
        ServiceManager<ViewModelBase> vmFactory
    )
    {
        this.vmFactory = vmFactory;

        ClientManager = clientManager;
        SelectImageCardViewModel = vmFactory.Get<SelectImageCardViewModel>();

        // Update our width and height when the image changes
        SelectImageCardViewModel
            .WhenPropertyChanged(card => card.CurrentBitmapSize)
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(propertyValue =>
            {
                if (!propertyValue.Value.IsEmpty)
                {
                    Width = propertyValue.Value.Width;
                    Height = propertyValue.Value.Height;
                }
            });
    }

    [RelayCommand]
    private async Task PreviewPreprocessor(ComfyAuxPreprocessor? preprocessor)
    {
        if (
            preprocessor is null
            || SelectImageCardViewModel.ImageSource is not { } imageSource
            || SelectImageCardViewModel.IsImageFileNotFound
        )
            return;

        var args = new InferenceQueueCustomPromptEventArgs();

        var images = SelectImageCardViewModel.GetInputImages();

        await ClientManager.UploadInputImageAsync(imageSource);

        var image = args.Nodes.AddTypedNode(
            new ComfyNodeBuilder.LoadImage
            {
                Name = args.Nodes.GetUniqueName("Preprocessor_LoadImage"),
                Image =
                    SelectImageCardViewModel.ImageSource?.GetHashGuidFileNameCached("Inference")
                    ?? throw new ValidationException("No ImageSource")
            }
        ).Output1;

        var aioPreprocessor = args.Nodes.AddTypedNode(
            new ComfyNodeBuilder.AIOPreprocessor
            {
                Name = args.Nodes.GetUniqueName("Preprocessor"),
                Image = image,
                Preprocessor = preprocessor.ToString(),
                Resolution = Width is <= 2048 and > 0 ? Width : 512
            }
        );

        args.Builder.Connections.OutputNodes.Add(
            args.Nodes.AddTypedNode(
                new ComfyNodeBuilder.PreviewImage
                {
                    Name = args.Nodes.GetUniqueName("Preprocessor_OutputImage"),
                    Images = aioPreprocessor.Output
                }
            )
        );

        // Queue
        Dispatcher.UIThread.Post(() => EventManager.Instance.OnInferenceQueueCustomPrompt(args));

        // We don't know when it's done so wait a bit?
        await Task.Delay(1000);
    }
}
