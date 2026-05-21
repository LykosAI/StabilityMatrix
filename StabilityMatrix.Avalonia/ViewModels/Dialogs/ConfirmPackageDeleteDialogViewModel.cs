using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(ConfirmPackageDeleteDialog))]
[ManagedService]
[RegisterTransient<ConfirmPackageDeleteDialogViewModel>]
public partial class ConfirmPackageDeleteDialogViewModel : ContentDialogViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid), nameof(ExpectedPackageName))]
    public required partial InstalledPackage Package { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    public partial string PackageName { get; set; } = string.Empty;

    public string? ExpectedPackageName => Package.DisplayName;
    public bool IsValid => ExpectedPackageName?.Equals(PackageName, StringComparison.Ordinal) ?? false;
    public string DeleteWarningText
    {
        get
        {
            var items = new List<string>
            {
                $"• The {ExpectedPackageName} application",
                $"• {(Package.PackageName == "ComfyUI" ? "Custom nodes" : "Extensions")}",
            };

            if (!Package.UseSharedOutputFolder)
                items.Add("• Images/outputs");

            if (Package.PreferredSharedFolderMethod is SharedFolderMethod.None)
                items.Add("• Models/checkpoints placed in the package's model folders");

            items.Add("• Any custom files in the package folder");

            return string.Join(Environment.NewLine, items);
        }
    }

    [RelayCommand]
    private async Task CopyExpectedPackageName()
    {
        await App.Clipboard?.SetTextAsync(ExpectedPackageName);
    }
}
