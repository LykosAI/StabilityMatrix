using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.PackageManager;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Packages.Extensions;

namespace StabilityMatrix.Avalonia.ViewModels.PackageManager;

[View(typeof(PackageExtensionBrowserView))]
[Transient]
[ManagedService]
public partial class PackageExtensionBrowserViewModel : ViewModelBase
{
    public PackagePair? PackagePair { get; set; }

    [ObservableProperty]
    private string searchFilter = string.Empty;

    [ObservableProperty]
    private int selectedItemsCount;

    [ObservableProperty]
    private bool isLoading;

    private SourceCache<PackageExtension, string> availableExtensionsSource =
        new(ext => ext.Author + ext.Title + ext.Reference);

    public IObservableCollection<SelectableItem<PackageExtension>> AvailableItems { get; } =
        new ObservableCollectionExtended<SelectableItem<PackageExtension>>();

    public IObservableCollection<SelectableItem<PackageExtension>> AvailableItemsFiltered { get; } =
        new ObservableCollectionExtended<SelectableItem<PackageExtension>>();

    public IObservableCollection<SelectableItem<PackageExtension>> SelectedAvailableItems { get; } =
        new ObservableCollectionExtended<SelectableItem<PackageExtension>>();

    private SourceCache<InstalledPackageExtension, string> installedExtensionsSource =
        new(
            ext =>
                ext.Paths.FirstOrDefault()?.ToString() ?? ext.GitRepositoryUrl ?? ext.GetHashCode().ToString()
        );

    public IObservableCollection<InstalledPackageExtension> InstalledExtensions { get; } =
        new ObservableCollectionExtended<InstalledPackageExtension>();

    public PackageExtensionBrowserViewModel()
    {
        var searchPredicate = this.WhenPropertyChanged(vm => vm.SearchFilter)
            .Select(
                change =>
                    string.IsNullOrWhiteSpace(change.Value)
                        ? _ => true
                        : new Func<SelectableItem<PackageExtension>, bool>(
                            ext => ext.Item.Title.Contains(change.Value, StringComparison.OrdinalIgnoreCase)
                        )
            )
            .AsObservable();

        var availableItemsChangeSet = availableExtensionsSource
            .Connect()
            .Transform(ext => new SelectableItem<PackageExtension>(ext))
            .Publish();

        availableItemsChangeSet
            .SortBy(x => x.Item.Title)
            .Bind(AvailableItems)
            .Filter(searchPredicate)
            .Bind(AvailableItemsFiltered)
            .Subscribe();

        availableItemsChangeSet
            .AutoRefresh(item => item.IsSelected)
            .Filter(item => item.IsSelected)
            .ForEachChange(OnSelectedItemsUpdate)
            .Bind(SelectedAvailableItems)
            .Subscribe();

        availableItemsChangeSet.Connect();

        SelectedAvailableItems
            .WhenPropertyChanged(x => x.Count)
            .Select(x => x.Value)
            .Subscribe(x => SelectedItemsCount = x);
    }

    public void AddExtensions(params PackageExtension[] packageExtensions)
    {
        availableExtensionsSource.AddOrUpdate(packageExtensions);
    }

    [RelayCommand]
    public async Task InstallSelectedExtensions()
    {
        var extensions = SelectedAvailableItems.Select(x => x.Item).ToArray();

        if (extensions.Length == 0)
            return;

        var steps = extensions
            .Select(
                ext =>
                    new InstallExtensionStep(
                        PackagePair!.BasePackage.ExtensionManager!,
                        PackagePair.InstalledPackage,
                        ext
                    )
            )
            .Cast<IPackageStep>()
            .ToArray();

        var runner = new PackageModificationRunner { ShowDialogOnStart = true, HideCloseButton = true };
        EventManager.Instance.OnPackageInstallProgressAdded(runner);
        await runner.ExecuteSteps(steps);
    }

    [RelayCommand]
    private async Task Refresh()
    {
        if (PackagePair is null)
            return;

        if (PackagePair.BasePackage.ExtensionManager is not { } extensionManager)
        {
            throw new NotSupportedException(
                $"The package {PackagePair.BasePackage} does not support extensions."
            );
        }

        IsLoading = true;

        try
        {
            if (Design.IsDesignMode)
            {
                await Task.Delay(250);
            }
            else
            {
                // Refresh installed
                var installedExtensions = await extensionManager.GetInstalledExtensionsAsync(
                    PackagePair.InstalledPackage
                );

                installedExtensionsSource.EditDiff(installedExtensions);

                // Refresh available
                var extensions = await extensionManager.GetManifestExtensionsAsync(
                    extensionManager.GetManifests(PackagePair.InstalledPackage)
                );

                availableExtensionsSource.EditDiff(extensions);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnSelectedItemsUpdate(Change<SelectableItem<PackageExtension>, string> change)
    {
        Debug.WriteLine($"{change}");
    }
}
