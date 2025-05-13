using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(EnvVarsDialog))]
[ManagedService]
[RegisterTransient<EnvVarsViewModel>]
public partial class EnvVarsViewModel : ContentDialogViewModelBase
{
    [ObservableProperty]
    private string title = Resources.Label_EnvironmentVariables;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(EnvVarsView))]
    private ObservableCollection<EnvVarKeyPair> envVars = new();

    public DataGridCollectionView EnvVarsView => new(EnvVars);

    [RelayCommand]
    private void AddRow()
    {
        EnvVars.Add(new EnvVarKeyPair());
    }

    [RelayCommand]
    private void RemoveSelectedRow(int selectedIndex)
    {
        try
        {
            EnvVars.RemoveAt(selectedIndex);
        }
        catch (ArgumentOutOfRangeException)
        {
            Debug.WriteLine($"RemoveSelectedRow: Index {selectedIndex} out of range");
        }
    }

    public override BetterContentDialog GetDialog()
    {
        var dialog = base.GetDialog();

        dialog.PrimaryButtonText = Resources.Action_Save;
        dialog.IsPrimaryButtonEnabled = true;
        dialog.CloseButtonText = Resources.Action_Cancel;

        return dialog;
    }
}
