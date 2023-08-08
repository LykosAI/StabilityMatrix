using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(SeedCard))]
public partial class SeedCardViewModel : ViewModelBase, ILoadableState<SeedCardModel>
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
    public void LoadState(SeedCardModel state)
    {
        Seed = long.TryParse(state.Seed, out var result) ? result : 0;
        IsRandomizeEnabled = state.IsRandomizeEnabled;
    }

    /// <inheritdoc />
    public SeedCardModel SaveState()
    {
        return new SeedCardModel
        {
            Seed = Seed.ToString(),
            IsRandomizeEnabled = IsRandomizeEnabled
        };
    }
}
