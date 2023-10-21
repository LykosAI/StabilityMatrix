using System.Linq;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(StackExpander))]
public partial class StackExpanderViewModel : StackViewModelBase
{
    public const string ModuleKey = "StackExpander";

    [ObservableProperty]
    [property: JsonIgnore]
    private string? title;

    [ObservableProperty]
    [property: JsonIgnore]
    private string? titleExtra;

    /// <summary>
    /// True if parent StackEditableCard is in edit mode (can drag to reorder)
    /// </summary>
    [ObservableProperty]
    [property: JsonIgnore]
    private bool isEditEnabled;

    [ObservableProperty]
    private bool isEnabled;

    /// <inheritdoc />
    public StackExpanderViewModel(ServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory) { }

    public override void OnContainerIndexChanged(int value)
    {
        TitleExtra = $"{value + 1}.";
    }

    /// <inheritdoc />
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        base.LoadStateFromJsonObject(state);

        if (
            state.TryGetPropertyValue(nameof(IsEnabled), out var isEnabledNode)
            && isEnabledNode is JsonValue jsonValue
            && jsonValue.TryGetValue(out bool isEnabledBool)
        )
        {
            IsEnabled = isEnabledBool;
        }
    }

    /// <inheritdoc />
    public override JsonObject SaveStateToJsonObject()
    {
        var state = base.SaveStateToJsonObject();
        state.Add(nameof(IsEnabled), IsEnabled);
        return state;
    }
}
