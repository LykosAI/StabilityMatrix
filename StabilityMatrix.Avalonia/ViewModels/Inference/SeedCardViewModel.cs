using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(SeedCard))]
public partial class SeedCardViewModel : ViewModelBase
{
    [ObservableProperty, NotifyPropertyChangedFor(nameof(RandomizeButtonToolTip))]
    private bool isRandomizeEnabled;
    
    [ObservableProperty] 
    private long seed;

    public string RandomizeButtonToolTip => IsRandomizeEnabled 
        ? "Seed is locked" 
        : "Randomizing Seed on each run";
    
    [RelayCommand]
    public void GenerateNewSeed()
    {
        Seed = Random.Shared.NextInt64();
    }
}
