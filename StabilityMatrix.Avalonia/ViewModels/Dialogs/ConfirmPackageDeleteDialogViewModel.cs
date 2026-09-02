using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;

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

    [ObservableProperty]
    public partial string? FolderSizeText { get; set; }

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

            // Symlink mode is the only one that relocates models out of the package folder;
            // in Configuration (yaml) and None modes they are real files that will be deleted
            if (Package.PreferredSharedFolderMethod is not SharedFolderMethod.Symlink)
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

    /// <inheritdoc />
    public override async Task OnLoadedAsync()
    {
        await base.OnLoadedAsync();

        if (Package.FullPath is not { } fullPath)
            return;

        try
        {
            var sizeBytes = await new DirectoryPath(fullPath).GetSizeAsync(includeSymbolicLinks: false);
            FolderSizeText = $"Total size: {Size.FormatBytes(Convert.ToUInt64(sizeBytes))}";
        }
        catch (Exception)
        {
            // Size display is informational only
        }
    }
}
