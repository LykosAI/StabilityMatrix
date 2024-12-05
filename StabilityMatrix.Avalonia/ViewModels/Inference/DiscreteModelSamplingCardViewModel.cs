using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(DiscreteModelSamplingCard))]
[ManagedService]
[Transient]
public partial class DiscreteModelSamplingCardViewModel : LoadableViewModelBase
{
    public const string ModuleKey = "DiscreteModelSampling";
    public ObservableCollection<string> SamplingMethods { get; set; } = ["eps", "v_prediction", "lcm", "x0"];

    [ObservableProperty]
    private bool isZsnrEnabled;

    [ObservableProperty]
    private string selectedSamplingMethod;

    public override JsonObject SaveStateToJsonObject() =>
        SerializeModel(
            new DiscreteModelSamplingCardModel
            {
                SamplingMethod = SelectedSamplingMethod,
                IsZsnrEnabled = IsZsnrEnabled
            }
        );

    public override void LoadStateFromJsonObject(JsonObject state)
    {
        var model = DeserializeModel<DiscreteModelSamplingCardModel>(state);
        SelectedSamplingMethod = model.SamplingMethod;
        IsZsnrEnabled = model.IsZsnrEnabled;
    }

    internal class DiscreteModelSamplingCardModel
    {
        public string SamplingMethod { get; set; }
        public bool IsZsnrEnabled { get; set; }
    }
}
