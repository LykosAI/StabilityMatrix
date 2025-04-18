using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(StackExpander))]
[ManagedService]
[RegisterTransient<StackExpanderViewModel>]
public partial class StackExpanderViewModel : StackViewModelBase
{
    public const string ModuleKey = "StackExpander";

    [ObservableProperty]
    [property: JsonIgnore]
    private string? title;

    [ObservableProperty]
    [property: JsonIgnore]
    private string? titleExtra;

    [ObservableProperty]
    private bool isEnabled;

    /// <summary>
    /// True if parent StackEditableCard is in edit mode (can drag to reorder)
    /// </summary>
    [ObservableProperty]
    [property: JsonIgnore]
    private bool isEditEnabled;

    /// <summary>
    /// True to show the settings button, invokes <see cref="SettingsCommand"/> when clicked
    /// </summary>
    public virtual bool IsSettingsEnabled { get; set; }

    public virtual IRelayCommand? SettingsCommand { get; set; }

    /// <inheritdoc />
    public StackExpanderViewModel(IServiceManager<ViewModelBase> vmFactory)
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
