using System;
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
