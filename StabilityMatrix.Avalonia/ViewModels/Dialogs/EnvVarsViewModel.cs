using System.Collections.Generic;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(EnvVarsViewModel))]
public partial class EnvVarsViewModel : ContentDialogViewModelBase
{
    [ObservableProperty]
    private string title = "Environment Variables";

    [ObservableProperty, NotifyPropertyChangedFor(nameof(EnvVarsView))]
    private IList<EnvVarKeyPair> envVars = new List<EnvVarKeyPair>();

    public DataGridCollectionView EnvVarsView => new(EnvVars);
    
    // Add new environment variable
    [RelayCommand]
    private void AddEnvVar()
    {
        EnvVars.Add(new EnvVarKeyPair());
    }
}
