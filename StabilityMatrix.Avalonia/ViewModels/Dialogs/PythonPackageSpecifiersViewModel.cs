using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[Transient]
[ManagedService]
[View(typeof(PythonPackageSpecifiersDialog))]
public partial class PythonPackageSpecifiersViewModel : ContentDialogViewModelBase
{
    [ObservableProperty]
    private string? title = Resources.Label_PythonDependenciesOverride_Title;

    [ObservableProperty]
    private string? description = Resources.Label_PythonDependenciesOverride_Description;

    [ObservableProperty]
    private string? helpLinkLabel = Resources.Label_DependencySpecifiers;

    [ObservableProperty]
    private Uri? helpLinkUri =
        new("https://packaging.python.org/en/latest/specifications/dependency-specifiers");

    protected ObservableCollection<PythonPackageSpecifiersItem> Specifiers { get; } = [];

    public DataGridCollectionView SpecifiersView { get; }

    public PythonPackageSpecifiersViewModel()
    {
        SpecifiersView = new DataGridCollectionView(Specifiers);
    }

    public void LoadSpecifiers(IEnumerable<PipPackageSpecifierOverride> specifiers)
    {
        Specifiers.Clear();
        Specifiers.AddRange(specifiers.Select(PythonPackageSpecifiersItem.FromSpecifier));
    }

    public IEnumerable<PipPackageSpecifierOverride> GetSpecifiers()
    {
        return Specifiers.Select(row => row.ToSpecifier());
    }

    public override BetterContentDialog GetDialog()
    {
        var dialog = base.GetDialog();
        dialog.PrimaryButtonText = Resources.Action_Save;
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.CloseButtonText = Resources.Action_Cancel;
        dialog.MaxDialogWidth = 800;
        dialog.FullSizeDesired = true;
        return dialog;
    }

    [RelayCommand]
    private void AddRow()
    {
        Specifiers.Add(
            PythonPackageSpecifiersItem.FromSpecifier(
                new PipPackageSpecifierOverride
                {
                    Action = PipPackageSpecifierOverrideAction.Update,
                    Constraint = "=="
                }
            )
        );
    }

    [RelayCommand]
    private void RemoveSelectedRow(int selectedIndex)
    {
        try
        {
            Specifiers.RemoveAt(selectedIndex);
        }
        catch (ArgumentOutOfRangeException)
        {
            Debug.WriteLine($"RemoveSelectedRow: Index {selectedIndex} out of range");
        }
    }
}
