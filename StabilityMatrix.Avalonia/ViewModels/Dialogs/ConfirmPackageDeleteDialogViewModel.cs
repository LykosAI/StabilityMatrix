using System;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using Injectio.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(ConfirmPackageDeleteDialog))]
[ManagedService]
[RegisterTransient<ConfirmPackageDeleteDialogViewModel>]
public partial class ConfirmPackageDeleteDialogViewModel : ContentDialogViewModelBase
{
    public required string ExpectedPackageName { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    private string packageName = string.Empty;

    public bool IsValid => ExpectedPackageName.Equals(PackageName, StringComparison.Ordinal);
}
