using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Collections;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views.PackageManager;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Packages.Extensions;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.PackageManager;

[View(typeof(PackageExtensionBrowserView))]
[Transient]
[ManagedService]
public partial class PackageExtensionBrowserViewModel : ViewModelBase, IDisposable
{
    private readonly INotificationService notificationService;
    private readonly ISettingsManager settingsManager;
    private readonly ServiceManager<ViewModelBase> vmFactory;
    private readonly CompositeDisposable cleanUp;

    public PackagePair? PackagePair { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoExtensionsFoundMessage))]
    private bool isLoading;

    private SourceCache<PackageExtension, string> availableExtensionsSource =
        new(ext => ext.Author + ext.Title + ext.Reference);

    public IObservableCollection<SelectableItem<PackageExtension>> SelectedAvailableItems { get; } =
        new ObservableCollectionExtended<SelectableItem<PackageExtension>>();

    public SearchCollection<
        SelectableItem<PackageExtension>,
        string,
        string
    > AvailableItemsSearchCollection { get; }

    private SourceCache<InstalledPackageExtension, string> installedExtensionsSource =
        new(
            ext =>
                ext.Paths.FirstOrDefault()?.ToString() ?? ext.GitRepositoryUrl ?? ext.GetHashCode().ToString()
        );

    public IObservableCollection<SelectableItem<InstalledPackageExtension>> SelectedInstalledItems { get; } =
        new ObservableCollectionExtended<SelectableItem<InstalledPackageExtension>>();

    public SearchCollection<
        SelectableItem<InstalledPackageExtension>,
        string,
        string
    > InstalledItemsSearchCollection { get; }

    public IObservableCollection<InstalledPackageExtension> InstalledExtensions { get; } =
        new ObservableCollectionExtended<InstalledPackageExtension>();

    [ObservableProperty]
    private bool showNoExtensionsFoundMessage;

    public PackageExtensionBrowserViewModel(
        INotificationService notificationService,
        ISettingsManager settingsManager,
        ServiceManager<ViewModelBase> vmFactory
    )
    {
        this.notificationService = notificationService;
        this.settingsManager = settingsManager;
        this.vmFactory = vmFactory;

        var availableItemsChangeSet = availableExtensionsSource
            .Connect()
            .Transform(ext => new SelectableItem<PackageExtension>(ext))
            .ObserveOn(SynchronizationContext.Current!)
            .Publish();

        availableItemsChangeSet
            .AutoRefresh(item => item.IsSelected)
            .Filter(item => item.IsSelected)
            .Bind(SelectedAvailableItems)
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe();

        var installedItemsChangeSet = installedExtensionsSource
            .Connect()
            .Transform(ext => new SelectableItem<InstalledPackageExtension>(ext))
            .ObserveOn(SynchronizationContext.Current!)
            .Publish();

        installedItemsChangeSet
            .AutoRefresh(item => item.IsSelected)
            .Filter(item => item.IsSelected)
            .Bind(SelectedInstalledItems)
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe();

        cleanUp = new CompositeDisposable(
            AvailableItemsSearchCollection = new SearchCollection<
                SelectableItem<PackageExtension>,
                string,
                string
            >(
                availableItemsChangeSet,
                query =>
                    string.IsNullOrWhiteSpace(query)
                        ? _ => true
                        : x => x.Item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            ),
            availableItemsChangeSet.Connect(),
            InstalledItemsSearchCollection = new SearchCollection<
                SelectableItem<InstalledPackageExtension>,
                string,
                string
            >(
                installedItemsChangeSet,
                query =>
                    string.IsNullOrWhiteSpace(query)
                        ? _ => true
                        : x => x.Item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            ),
            installedItemsChangeSet.Connect()
        );
    }

    public void AddExtensions(
        IEnumerable<PackageExtension> packageExtensions,
        IEnumerable<InstalledPackageExtension> installedExtensions
    )
    {
        availableExtensionsSource.AddOrUpdate(packageExtensions);
        installedExtensionsSource.AddOrUpdate(installedExtensions);
    }

    [RelayCommand]
    public async Task InstallSelectedExtensions()
    {
        if (!await BeforeInstallCheck())
            return;

        var extensions = SelectedAvailableItems
            .Select(item => item.Item)
            .Where(extension => !extension.IsInstalled)
            .ToArray();

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

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            ModificationCompleteTitle = "Installed Extensions",
            ModificationCompleteMessage = "Finished installing extensions"
        };
        EventManager.Instance.OnPackageInstallProgressAdded(runner);

        await runner.ExecuteSteps(steps);

        ClearSelection();

        RefreshBackground();
    }

    [RelayCommand]
    public async Task UpdateSelectedExtensions()
    {
        var extensions = SelectedInstalledItems.Select(x => x.Item).ToArray();

        if (extensions.Length == 0)
            return;

        var steps = extensions
            .Select(
                ext =>
                    new UpdateExtensionStep(
                        PackagePair!.BasePackage.ExtensionManager!,
                        PackagePair.InstalledPackage,
                        ext
                    )
            )
            .Cast<IPackageStep>()
            .ToArray();

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            ModificationCompleteTitle = "Updated Extensions",
            ModificationCompleteMessage = "Finished updating extensions"
        };
        EventManager.Instance.OnPackageInstallProgressAdded(runner);

        await runner.ExecuteSteps(steps);

        ClearSelection();

        RefreshBackground();
    }

    [RelayCommand]
    public async Task UninstallSelectedExtensions()
    {
        var extensions = SelectedInstalledItems.Select(x => x.Item).ToArray();

        if (extensions.Length == 0)
            return;

        var steps = extensions
            .Select(
                ext =>
                    new UninstallExtensionStep(
                        PackagePair!.BasePackage.ExtensionManager!,
                        PackagePair.InstalledPackage,
                        ext
                    )
            )
            .Cast<IPackageStep>()
            .ToArray();

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            ModificationCompleteTitle = "Uninstalled Extensions",
            ModificationCompleteMessage = "Finished uninstalling extensions"
        };
        EventManager.Instance.OnPackageInstallProgressAdded(runner);

        await runner.ExecuteSteps(steps);

        ClearSelection();

        RefreshBackground();
    }

    [RelayCommand]
    public async Task OpenExtensionsSettingsDialog()
    {
        if (PackagePair is null)
            return;

        var grid = new ExtensionSettingsPropertyGrid
        {
            ManifestUrls = new BindingList<string>(
                PackagePair?.InstalledPackage.ExtraExtensionManifestUrls ?? []
            )
        };

        var dialog = vmFactory
            .Get<PropertyGridViewModel>(vm =>
            {
                vm.Title = $"{Resources.Label_Settings}";
                vm.SelectedObject = grid;
                vm.IncludeCategories = ["Base"];
            })
            .GetSaveDialog();

        dialog.MinDialogWidth = 750;
        dialog.MaxDialogWidth = 750;

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await using var _ = settingsManager.BeginTransaction();

            PackagePair!.InstalledPackage.ExtraExtensionManifestUrls = grid.ManifestUrls.ToList();
        }
    }

    /// <inheritdoc />
    public override async Task OnLoadedAsync()
    {
        await base.OnLoadedAsync();

        await Refresh();
    }

    [RelayCommand]
    public async Task Refresh()
    {
        if (PackagePair is null)
            return;

        IsLoading = true;

        try
        {
            if (Design.IsDesignMode)
            {
                var (availableExtensions, installedExtensions) = SynchronizeExtensions(
                    availableExtensionsSource.Items,
                    installedExtensionsSource.Items
                );

                availableExtensionsSource.EditDiff(availableExtensions);
                installedExtensionsSource.EditDiff(installedExtensions);

                await Task.Delay(250);
            }
            else
            {
                await RefreshCore();
            }
        }
        finally
        {
            IsLoading = false;
            ShowNoExtensionsFoundMessage = AvailableItemsSearchCollection.FilteredItems.Count == 0;
        }
    }

    public void RefreshBackground()
    {
        RefreshCore()
            .SafeFireAndForget(ex =>
            {
                notificationService.ShowPersistent("Failed to refresh extensions", ex.ToString());
            });
    }

    private async Task RefreshCore()
    {
        using var _ = CodeTimer.StartDebug();

        if (PackagePair?.BasePackage.ExtensionManager is not { } extensionManager)
        {
            throw new NotSupportedException(
                $"The package {PackagePair?.BasePackage} does not support extensions."
            );
        }

        var availableExtensions = (
            await extensionManager.GetManifestExtensionsAsync(
                extensionManager.GetManifests(PackagePair.InstalledPackage)
            )
        ).ToArray();

        var installedExtensions = (
            await extensionManager.GetInstalledExtensionsAsync(PackagePair.InstalledPackage)
        ).ToArray();

        // Synchronize
        SynchronizeExtensions(availableExtensions, installedExtensions);

        await Task.Run(() =>
        {
            availableExtensionsSource.Edit(updater =>
            {
                updater.Load(availableExtensions);
            });

            installedExtensionsSource.Edit(updater =>
            {
                updater.Load(installedExtensions);
            });
        });
    }

    public void ClearSelection()
    {
        foreach (var item in SelectedAvailableItems.ToImmutableArray())
        {
            item.IsSelected = false;
        }

        foreach (var item in SelectedInstalledItems.ToImmutableArray())
        {
            item.IsSelected = false;
        }
    }

    private async Task<bool> BeforeInstallCheck()
    {
        if (
            !settingsManager.Settings.SeenTeachingTips.Contains(
                Core.Models.Settings.TeachingTip.PackageExtensionsInstallNotice
            )
        )
        {
            var dialog = new BetterContentDialog
            {
                Title = "Installing Extensions",
                Content = """
                          Extensions, the extension index, and their dependencies are community provided and not verified by the Stability Matrix team. 
                          
                          The install process may invoke external programs and scripts.
                          
                          Please review the extension's source code and applicable licenses before installing.
                          """,
                PrimaryButtonText = Resources.Action_Continue,
                CloseButtonText = Resources.Action_Cancel,
                DefaultButton = ContentDialogButton.Primary,
                MaxDialogWidth = 400
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return false;
            }

            settingsManager.Transaction(
                s => s.SeenTeachingTips.Add(Core.Models.Settings.TeachingTip.PackageExtensionsInstallNotice)
            );
        }

        return true;
    }

    [Pure]
    private static (
        IEnumerable<PackageExtension> extensions,
        IEnumerable<InstalledPackageExtension> installedExtensions
    ) SynchronizeExtensions(
        IEnumerable<PackageExtension> extensions,
        IEnumerable<InstalledPackageExtension> installedExtensions
    )
    {
        var availableArr = extensions.ToArray();
        var installedArr = installedExtensions.ToArray();

        SynchronizeExtensions(availableArr, installedArr);

        return (availableArr, installedArr);
    }

    private static void SynchronizeExtensions(
        IList<PackageExtension> extensions,
        IList<InstalledPackageExtension> installedExtensions
    )
    {
        // For extensions, map their file paths for lookup
        var repoToExtension = extensions
            .SelectMany(ext => ext.Files.Select(path => (path, ext)))
            .ToLookup(kv => kv.path.ToString().StripEnd(".git"))
            .ToDictionary(group => group.Key, x => x.First().ext);

        // For installed extensions, add remote repo if available
        var extensionsInstalled = new HashSet<PackageExtension>();

        foreach (var (i, installedExt) in installedExtensions.Enumerate())
        {
            if (
                installedExt.GitRepositoryUrl is not null
                && repoToExtension.TryGetValue(
                    installedExt.GitRepositoryUrl.StripEnd(".git"),
                    out var mappedExt
                )
            )
            {
                extensionsInstalled.Add(mappedExt);

                installedExtensions[i] = installedExt with { Definition = mappedExt };
            }
        }

        // For available extensions, add installed status if available
        foreach (var (i, ext) in extensions.Enumerate())
        {
            if (extensionsInstalled.Contains(ext))
            {
                extensions[i] = ext with { IsInstalled = true };
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        availableExtensionsSource.Dispose();
        installedExtensionsSource.Dispose();

        cleanUp.Dispose();

        GC.SuppressFinalize(this);
    }

    private class ExtensionSettingsPropertyGrid : AbstractNotifyPropertyChanged
    {
        [Category("Base")]
        [DisplayName("Extension Manifest Sources")]
        public BindingList<string> ManifestUrls { get; init; } = [];
    }
}
