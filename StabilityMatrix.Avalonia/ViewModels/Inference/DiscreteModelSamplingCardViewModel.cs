using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(DiscreteModelSamplingCard))]
[ManagedService]
[RegisterTransient<DiscreteModelSamplingCardViewModel>]
public partial class DiscreteModelSamplingCardViewModel : LoadableViewModelBase
{
    public const string ModuleKey = "DiscreteModelSampling";
    public List<string> SamplingMethods => ["eps", "v_prediction", "lcm", "x0"];

    [ObservableProperty]
    private bool isZsnrEnabled;

    [ObservableProperty]
    private string selectedSamplingMethod = "eps";
}
