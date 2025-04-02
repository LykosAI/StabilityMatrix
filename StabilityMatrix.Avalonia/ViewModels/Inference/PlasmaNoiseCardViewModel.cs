using CommunityToolkit.Mvvm.ComponentModel;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(PlasmaNoiseCard))]
[ManagedService]
[RegisterTransient<PlasmaNoiseCardViewModel>]
public partial class PlasmaNoiseCardViewModel : LoadableViewModelBase
{
    public const string ModuleKey = "PlasmaNoise";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPlasmaTurbulence))]
    private NoiseType selectedNoiseType = NoiseType.Plasma;

    [ObservableProperty]
    private double plasmaTurbulence = 2.75;

    [ObservableProperty]
    private int valueMin = -1;

    [ObservableProperty]
    private int valueMax = -1;

    [ObservableProperty]
    private bool isPerChannelClampingEnabled;

    [ObservableProperty]
    private bool isPlasmaSamplerEnabled;

    [ObservableProperty]
    private int redMin = -1;

    [ObservableProperty]
    private int redMax = -1;

    [ObservableProperty]
    private int greenMin = -1;

    [ObservableProperty]
    private int greenMax = -1;

    [ObservableProperty]
    private int blueMin = -1;

    [ObservableProperty]
    private int blueMax = -1;

    [ObservableProperty]
    private double plasmaSamplerLatentNoise = 0.05;

    public List<NoiseType> NoiseTypes => Enum.GetValues<NoiseType>().ToList();
    public bool ShowPlasmaTurbulence => SelectedNoiseType == NoiseType.Plasma;
}
