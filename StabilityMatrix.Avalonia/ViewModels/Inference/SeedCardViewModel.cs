using System;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(SeedCard))]
public partial class SeedCardViewModel : LoadableViewModelBase
{
    [ObservableProperty, NotifyPropertyChangedFor(nameof(RandomizeButtonToolTip))]
    private bool isRandomizeEnabled = true;
    
    [ObservableProperty] 
    private long seed;

    public string RandomizeButtonToolTip => IsRandomizeEnabled 
        ? "Randomizing Seed on each run"
        : "Seed is locked";
    
    [RelayCommand]
    public void GenerateNewSeed()
    {
        Seed = Random.Shared.NextInt64();
    }

    /// <inheritdoc />
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        var model = DeserializeModel<SeedCardModel>(state);
        
        Seed = long.TryParse(model.Seed, out var result) ? result : 0;
        IsRandomizeEnabled = model.IsRandomizeEnabled;
    }

    /// <inheritdoc />
    public override JsonObject SaveStateToJsonObject()
    {
        return SerializeModel(new SeedCardModel
        {
            Seed = Seed.ToString(),
            IsRandomizeEnabled = IsRandomizeEnabled
        });
    }
}
