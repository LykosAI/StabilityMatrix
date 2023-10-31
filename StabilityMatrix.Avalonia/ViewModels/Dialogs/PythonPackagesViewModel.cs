using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(PythonPackagesDialog))]
[ManagedService]
[Transient]
public partial class PythonPackagesViewModel : ContentDialogViewModelBase
{
    public DirectoryPath? VenvPath { get; set; }

    public bool IsLoading { get; set; }

    private readonly SourceCache<PipPackageInfo, string> packageSource = new(p => p.Name);

    public IObservableCollection<PythonPackagesItemViewModel> Packages { get; } =
        new ObservableCollectionExtended<PythonPackagesItemViewModel>();

    [ObservableProperty]
    private PythonPackagesItemViewModel? selectedPackage;

    public PythonPackagesViewModel()
    {
        packageSource
            .Connect()
            .DeferUntilLoaded()
            .Transform(p => new PythonPackagesItemViewModel { Package = p })
            .Bind(Packages)
            .Subscribe();
    }

    public async Task Refresh()
    {
        if (VenvPath is null)
            return;

        IsLoading = true;

        try
        {
            await using var venvRunner = new PyVenvRunner(VenvPath);

            var packages = await venvRunner.PipList();
            packageSource.EditDiff(packages);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Load the selected package's show info if not already loaded
    /// </summary>
    partial void OnSelectedPackageChanged(PythonPackagesItemViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        if (value.PipShowResult is null)
        {
            value.LoadPipShowResult(VenvPath!).SafeFireAndForget();
        }
    }

    /// <inheritdoc />
    public override void OnLoaded()
    {
        Dispatcher.UIThread.InvokeAsync(Refresh).SafeFireAndForget();
    }

    /*/// <inheritdoc />
    public override async Task OnLoadedAsync()
    {
        await Refresh();
    }*/

    public void AddPackages(params PipPackageInfo[] packages)
    {
        packageSource.AddOrUpdate(packages);
    }

    [RelayCommand]
    private async Task InstallPackage()
    {
        if (VenvPath is null)
            return;

        // Dialog
        var fields = new TextBoxField[]
        {
            new() { Label = "Package Name", InnerLeftText = "pip install" }
        };

        var dialog = DialogHelper.CreateTextEntryDialog("Install Package", "", fields);
        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary || fields[0].Text is not { } packageName)
        {
            return;
        }

        var steps = new List<IPackageStep>
        {
            new PipStep
            {
                VenvDirectory = VenvPath,
                WorkingDirectory = VenvPath.Parent,
                Args = new[] { "install", "--yes", packageName }
            }
        };

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            HideCloseButton = true,
        };
        EventManager.Instance.OnPackageInstallProgressAdded(runner);
        await runner.ExecuteSteps(steps);

        // Refresh
        Dispatcher.UIThread.InvokeAsync(Refresh).SafeFireAndForget();
    }

    [RelayCommand]
    private async Task UninstallSelectedPackage()
    {
        if (VenvPath is null || SelectedPackage?.Package is not { } package)
            return;

        // Confirmation dialog
        var dialog = DialogHelper.CreateMarkdownDialog(
            $"This will uninstall the package '{package.Name}'",
            Resources.Label_ConfirmQuestion
        );
        dialog.PrimaryButtonText = Resources.Action_Uninstall;
        dialog.IsPrimaryButtonEnabled = true;
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.CloseButtonText = Resources.Action_Cancel;

        if (await dialog.ShowAsync() is not ContentDialogResult.Primary)
        {
            return;
        }

        var steps = new List<IPackageStep>
        {
            new PipStep
            {
                VenvDirectory = VenvPath,
                WorkingDirectory = VenvPath.Parent,
                Args = new[] { "uninstall", "--yes", package.Name }
            }
        };

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            HideCloseButton = true,
        };
        EventManager.Instance.OnPackageInstallProgressAdded(runner);
        await runner.ExecuteSteps(steps);

        // Refresh
        Dispatcher.UIThread.InvokeAsync(Refresh).SafeFireAndForget();
    }

    public BetterContentDialog GetDialog()
    {
        return new BetterContentDialog
        {
            CloseOnClickOutside = true,
            MinDialogWidth = 800,
            MaxDialogWidth = 1500,
            FullSizeDesired = true,
            ContentVerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Title = Resources.Label_PythonPackages,
            Content = new PythonPackagesDialog { DataContext = this },
            CloseButtonText = Resources.Action_Close,
            DefaultButton = ContentDialogButton.Close
        };
    }
}
