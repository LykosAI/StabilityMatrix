using System;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(SeedCard))]
[ManagedService]
[RegisterTransient<SeedCardViewModel>]
public partial class SeedCardViewModel : LoadableViewModelBase
{
    [ObservableProperty, NotifyPropertyChangedFor(nameof(RandomizeButtonToolTip))]
    private bool isRandomizeEnabled = true;

    [ObservableProperty]
    private long seed;

    public string RandomizeButtonToolTip =>
        IsRandomizeEnabled ? "Randomizing Seed on each run" : "Seed is locked";

    [RelayCommand]
    public void GenerateNewSeed()
    {
        Seed = Random.Shared.NextInt64(0, int.MaxValue);
    }

    /// <inheritdoc />
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        var model = DeserializeModel<SeedCardModel>(state);

        Seed = model.Seed;
        IsRandomizeEnabled = model.IsRandomizeEnabled;
    }

    /// <inheritdoc />
    public override JsonObject SaveStateToJsonObject()
    {
        return SerializeModel(new SeedCardModel { Seed = Seed, IsRandomizeEnabled = IsRandomizeEnabled });
    }
}
