using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(FaceDetailerCard))]
[ManagedService]
[RegisterTransient<FaceDetailerViewModel>]
public partial class FaceDetailerViewModel : LoadableViewModelBase
{
    private readonly IServiceManager<ViewModelBase> vmFactory;
    public const string ModuleKey = "FaceDetailer";

    [ObservableProperty]
    private bool guideSizeFor = true;

    [ObservableProperty]
    private int guideSize = 256;

    [ObservableProperty]
    private int maxSize = 768;

    [ObservableProperty]
    [property: Category("Settings"), DisplayName("Step Count Selection")]
    private bool isStepsEnabled;

    [ObservableProperty]
    private int steps = 20;

    [ObservableProperty]
    [property: Category("Settings"), DisplayName("CFG Scale Selection")]
    private bool isCfgScaleEnabled;

    [ObservableProperty]
    private double cfg = 8;

    [ObservableProperty]
    [property: Category("Settings"), DisplayName("Sampler Selection")]
    private bool isSamplerSelectionEnabled;

    [ObservableProperty]
    private ComfySampler? sampler = ComfySampler.Euler;

    [ObservableProperty]
    [property: Category("Settings"), DisplayName("Scheduler Selection")]
    private bool isSchedulerSelectionEnabled;

    [ObservableProperty]
    private ComfyScheduler? scheduler = ComfyScheduler.Normal;

    [ObservableProperty]
    private double denoise = 0.5d;

    [ObservableProperty]
    private int feather = 5;

    [ObservableProperty]
    private bool noiseMask = true;

    [ObservableProperty]
    private bool forceInpaint = false;

    [ObservableProperty]
    private double bboxThreshold = 0.5d;

    [ObservableProperty]
    private int bboxDilation = 10;

    [ObservableProperty]
    private int bboxCropFactor = 3;

    [ObservableProperty]
    private string samDetectionHint = "center-1";

    [ObservableProperty]
    private int samDilation = 0;

    [ObservableProperty]
    private double samThreshold = 0.93d;

    [ObservableProperty]
    private int samBboxExpansion = 0;

    [ObservableProperty]
    private double samMaskHintThreshold = 0.7d;

    [ObservableProperty]
    private string samMaskHintUseNegative = "False";

    [ObservableProperty]
    private int dropSize = 10;

    [ObservableProperty]
    private int cycle = 1;

    [ObservableProperty]
    private HybridModelFile? bboxModel;

    [ObservableProperty]
    private HybridModelFile? segmModel;

    [ObservableProperty]
    private HybridModelFile? samModel;

    [ObservableProperty]
    private bool showSamModelSelector = true;

    [ObservableProperty]
    private bool useSeparatePrompt;

    [ObservableProperty]
    private string positivePrompt = string.Empty;

    [ObservableProperty]
    private string negativePrompt = string.Empty;

    [ObservableProperty]
    [property: Category("Settings"), DisplayName("Inherit Seed")]
    private bool inheritSeed = true;

    [ObservableProperty]
    public partial bool UseTiledEncode { get; set; }

    [ObservableProperty]
    public partial bool UseTiledDecode { get; set; }

    public IReadOnlyList<ComfyScheduler> AvailableSchedulers => ComfyScheduler.FaceDetailerDefaults;

    /// <inheritdoc/>
    public FaceDetailerViewModel(
        IInferenceClientManager clientManager,
        IServiceManager<ViewModelBase> vmFactory
    )
    {
        this.vmFactory = vmFactory;
        ClientManager = clientManager;
        SeedCardViewModel = vmFactory.Get<SeedCardViewModel>();
        SeedCardViewModel.GenerateNewSeed();
        PromptCardViewModel = vmFactory.Get<PromptCardViewModel>();
        WildcardViewModel = vmFactory.Get<PromptCardViewModel>(vm =>
        {
            vm.IsNegativePromptEnabled = false;
            vm.IsStackCardEnabled = false;
        });
    }

    [JsonPropertyName("DetailerSeed")]
    public SeedCardViewModel SeedCardViewModel { get; }

    [JsonPropertyName("DetailerPrompt")]
    public PromptCardViewModel PromptCardViewModel { get; }

    [JsonPropertyName("DetailerWildcard")]
    public PromptCardViewModel WildcardViewModel { get; }

    public ObservableCollection<string> SamDetectionHints { get; set; } =
        [
            "center-1",
            "horizontal-2",
            "vertical-2",
            "rect-4",
            "diamond-4",
            "mask-area",
            "mask-points",
            "mask-point-bbox",
            "none",
        ];

    public ObservableCollection<string> SamMaskHintUseNegatives { get; set; } = ["False", "Small", "Outter"];

    public IInferenceClientManager ClientManager { get; }
}
