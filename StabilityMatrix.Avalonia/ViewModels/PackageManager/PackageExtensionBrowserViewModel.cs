using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Collections;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Controls;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views.PackageManager;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Git;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Packages.Extensions;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.PackageManager;

[View(typeof(PackageExtensionBrowserView))]
[RegisterTransient<PackageExtensionBrowserViewModel>]
[ManagedService]
public partial class PackageExtensionBrowserViewModel : ViewModelBase, IDisposable
{
    private readonly INotificationService notificationService;
    private readonly ISettingsManager settingsManager;
    private readonly IServiceManager<ViewModelBase> vmFactory;
    private readonly IPrerequisiteHelper prerequisiteHelper;
    private readonly CompositeDisposable cleanUp;

    public PackagePair? PackagePair { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoExtensionsFoundMessage))]
    private bool isLoading;

    private SourceCache<PackageExtension, string> availableExtensionsSource = new(ext =>
        ext.Author + ext.Title + ext.Reference
    );

    public IObservableCollection<SelectableItem<PackageExtension>> SelectedAvailableItems { get; } =
        new ObservableCollectionExtended<SelectableItem<PackageExtension>>();

    public SearchCollection<
        SelectableItem<PackageExtension>,
        string,
        string
    > AvailableItemsSearchCollection { get; }

    private SourceCache<InstalledPackageExtension, string> installedExtensionsSource = new(ext =>
        ext.Paths.FirstOrDefault()?.ToString() ?? ext.GitRepositoryUrl ?? ext.GetHashCode().ToString()
    );

    public IObservableCollection<SelectableItem<InstalledPackageExtension>> SelectedInstalledItems { get; } =
        new ObservableCollectionExtended<SelectableItem<InstalledPackageExtension>>();

    public SearchCollection<
        SelectableItem<InstalledPackageExtension>,
        string,
        string
    > InstalledItemsSearchCollection { get; }

    private SourceCache<ExtensionPack, string> extensionPackSource = new(ext => ext.Name);

    public IObservableCollection<ExtensionPack> ExtensionPacks { get; } =
        new ObservableCollectionExtended<ExtensionPack>();

    private SourceCache<SavedPackageExtension, string> extensionPackExtensionsSource = new(ext =>
        ext.PackageExtension.Author + ext.PackageExtension.Title + ext.PackageExtension.Reference
    );

    public IObservableCollection<
        SelectableItem<SavedPackageExtension>
    > SelectedExtensionPackExtensions { get; } =
        new ObservableCollectionExtended<SelectableItem<SavedPackageExtension>>();

    public SearchCollection<
        SelectableItem<SavedPackageExtension>,
        string,
        string
    > ExtensionPackExtensionsSearchCollection { get; }

    [ObservableProperty]
    private ExtensionPack? selectedExtensionPack;

    [ObservableProperty]
    private bool showNoExtensionsFoundMessage;

    [ObservableProperty]
    private bool areExtensionPacksLoading;

    public PackageExtensionBrowserViewModel(
        INotificationService notificationService,
        ISettingsManager settingsManager,
        IServiceManager<ViewModelBase> vmFactory,
        IPrerequisiteHelper prerequisiteHelper
    )
    {
        this.notificationService = notificationService;
        this.settingsManager = settingsManager;
        this.vmFactory = vmFactory;
        this.prerequisiteHelper = prerequisiteHelper;

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

        extensionPackSource
            .Connect()
            .Bind(ExtensionPacks)
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe();

        var extensionPackExtensionsChangeSet = extensionPackExtensionsSource
            .Connect()
            .Transform(ext => new SelectableItem<SavedPackageExtension>(ext))
            .ObserveOn(SynchronizationContext.Current!)
            .Publish();

        extensionPackExtensionsChangeSet
            .AutoRefresh(item => item.IsSelected)
            .Filter(item => item.IsSelected)
            .Bind(SelectedExtensionPackExtensions)
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
            installedItemsChangeSet.Connect(),
            ExtensionPackExtensionsSearchCollection = new SearchCollection<
                SelectableItem<SavedPackageExtension>,
                string,
                string
            >(
                extensionPackExtensionsChangeSet,
                query =>
                    string.IsNullOrWhiteSpace(query)
                        ? _ => true
                        : x =>
                            x.Item.PackageExtension.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            ),
            extensionPackExtensionsChangeSet.Connect()
        );
    }

    /// <inheritdoc />
    public override async Task OnLoadedAsync()
    {
        await base.OnLoadedAsync();
        await LoadExtensionPacksAsync();
        await Refresh();
    }

    public void AddExtensions(
        IEnumerable<PackageExtension> packageExtensions,
        IEnumerable<InstalledPackageExtension> installedExtensions
    )
    {
        availableExtensionsSource.AddOrUpdate(packageExtensions);
        installedExtensionsSource.AddOrUpdate(installedExtensions);
    }

    public void AddExtensionPacks(IEnumerable<ExtensionPack> packs)
    {
        extensionPackSource.AddOrUpdate(packs);
        SelectedExtensionPack = packs.FirstOrDefault();
        if (SelectedExtensionPack == null)
            return;

        extensionPackExtensionsSource.AddOrUpdate(SelectedExtensionPack.Extensions);
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
            .Select(ext => new InstallExtensionStep(
                PackagePair!.BasePackage.ExtensionManager!,
                PackagePair.InstalledPackage,
                ext
            ))
            .Cast<IPackageStep>()
            .ToArray();

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            ModificationCompleteTitle = "Installed Extensions",
            ModificationCompleteMessage = "Finished installing extensions",
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
            .Select(ext => new UpdateExtensionStep(
                PackagePair!.BasePackage.ExtensionManager!,
                PackagePair.InstalledPackage,
                ext
            ))
            .Cast<IPackageStep>()
            .ToArray();

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            ModificationCompleteTitle = "Updated Extensions",
            ModificationCompleteMessage = "Finished updating extensions",
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
            .Select(ext => new UninstallExtensionStep(
                PackagePair!.BasePackage.ExtensionManager!,
                PackagePair.InstalledPackage,
                ext
            ))
            .Cast<IPackageStep>()
            .ToArray();

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            ModificationCompleteTitle = "Uninstalled Extensions",
            ModificationCompleteMessage = "Finished uninstalling extensions",
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
            ),
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

    [RelayCommand]
    private async Task InstallExtensionPack()
    {
        if (SelectedExtensionPack == null)
            return;

        var steps = new List<IPackageStep>();

        foreach (var extension in SelectedExtensionPack.Extensions)
        {
            var installedExtension = installedExtensionsSource.Items.FirstOrDefault(x =>
                x.Definition?.Title == extension.PackageExtension.Title
                && x.Definition.Reference == extension.PackageExtension.Reference
            );

            if (installedExtension != null)
            {
                steps.Add(
                    new UpdateExtensionStep(
                        PackagePair!.BasePackage.ExtensionManager!,
                        PackagePair.InstalledPackage,
                        installedExtension,
                        extension.Version
                    )
                );
            }
            else
            {
                steps.Add(
                    new InstallExtensionStep(
                        PackagePair!.BasePackage.ExtensionManager!,
                        PackagePair!.InstalledPackage,
                        extension.PackageExtension,
                        extension.Version
                    )
                );
            }
        }

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            CloseWhenFinished = true,
            ModificationCompleteMessage = $"Extension Pack {SelectedExtensionPack.Name} installed",
        };

        EventManager.Instance.OnPackageInstallProgressAdded(runner);
        await runner.ExecuteSteps(steps);
        await Refresh();
    }

    [RelayCommand]
    public async Task CreateExtensionPackFromInstalled()
    {
        var extensions = SelectedInstalledItems.Select(x => x.Item).ToArray();
        if (extensions.Length == 0)
            return;

        var (dialog, nameField) = GetNameEntryDialog();
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var name = nameField.Text;
            var newExtensionPack = new ExtensionPack
            {
                Name = name,
                PackageType = PackagePair!.InstalledPackage.PackageName,
                Extensions = SelectedInstalledItems
                    .Where(x => x.Item.Definition != null)
                    .Select(x => new SavedPackageExtension
                    {
                        PackageExtension = x.Item.Definition,
                        Version = x.Item.Version,
                    })
                    .ToList(),
            };

            SaveExtensionPack(newExtensionPack, name);
            await LoadExtensionPacksAsync();
            notificationService.Show("Extension Pack Created", "The extension pack has been created");
        }
    }

    [RelayCommand]
    public async Task CreateExtensionPackFromAvailable()
    {
        var extensions = SelectedAvailableItems.Select(x => x.Item).ToArray();
        if (extensions.Length == 0)
            return;

        var (dialog, nameField) = GetNameEntryDialog();
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var name = nameField.Text;
            var newExtensionPack = new ExtensionPack
            {
                Name = name,
                PackageType = PackagePair!.InstalledPackage.PackageName,
                Extensions = SelectedAvailableItems
                    .Select(x => new SavedPackageExtension { PackageExtension = x.Item, Version = null })
                    .ToList(),
            };

            SaveExtensionPack(newExtensionPack, name);
            await LoadExtensionPacksAsync();
            notificationService.Show("Extension Pack Created", "The extension pack has been created");
        }
    }

    [RelayCommand]
    public async Task AddInstalledExtensionToPack(ExtensionPack pack)
    {
        foreach (var extension in SelectedInstalledItems)
        {
            if (
                pack.Extensions.Any(x =>
                    x.PackageExtension.Title == extension.Item.Definition?.Title
                    && x.PackageExtension.Author == extension.Item.Definition?.Author
                    && x.PackageExtension.Reference == extension.Item.Definition?.Reference
                )
            )
            {
                continue;
            }

            pack.Extensions.Add(
                new SavedPackageExtension
                {
                    PackageExtension = extension.Item.Definition!,
                    Version = extension.Item.Version,
                }
            );
        }

        SaveExtensionPack(pack, pack.Name);
        ClearSelection();
        await LoadExtensionPacksAsync();
        notificationService.Show(
            "Extensions added to pack",
            "The selected extensions have been added to the pack"
        );
    }

    [RelayCommand]
    public async Task AddExtensionToPack(ExtensionPack pack)
    {
        foreach (var extension in SelectedAvailableItems)
        {
            if (
                pack.Extensions.Any(x =>
                    x.PackageExtension.Title == extension.Item.Title
                    && x.PackageExtension.Author == extension.Item.Author
                    && x.PackageExtension.Reference == extension.Item.Reference
                )
            )
            {
                continue;
            }

            pack.Extensions.Add(
                new SavedPackageExtension { PackageExtension = extension.Item, Version = null }
            );
        }

        SaveExtensionPack(pack, pack.Name);
        ClearSelection();
        await LoadExtensionPacksAsync();
        notificationService.Show(
            "Extensions added to pack",
            "The selected extensions have been added to the pack"
        );
    }

    [RelayCommand]
    public async Task RemoveExtensionFromPack()
    {
        if (SelectedExtensionPack is null)
            return;

        foreach (var extension in SelectedExtensionPackExtensions)
        {
            extensionPackExtensionsSource.Remove(extension.Item);
            SelectedExtensionPack.Extensions.Remove(extension.Item);
        }

        SaveExtensionPack(SelectedExtensionPack, SelectedExtensionPack.Name);
        ClearSelection();
        await LoadExtensionPacksAsync();
    }

    [RelayCommand]
    private async Task DeleteExtensionPackAsync(ExtensionPack pack)
    {
        var pathToDelete = settingsManager
            .ExtensionPackDirectory.JoinDir(pack.PackageType)
            .JoinFile($"{pack.Name}.json");

        var confirmDeleteVm = vmFactory.Get<ConfirmDeleteDialogViewModel>();
        confirmDeleteVm.PathsToDelete = [pathToDelete];

        if (await confirmDeleteVm.GetDialog().ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await confirmDeleteVm.ExecuteCurrentDeleteOperationAsync(failFast: true);
        }
        catch (Exception e)
        {
            notificationService.ShowPersistent("Error deleting files", e.Message, NotificationType.Error);
            return;
        }

        ClearSelection();
        extensionPackSource.Remove(pack);
    }

    [RelayCommand]
    private async Task OpenExtensionPackFolder()
    {
        var extensionPackDir = settingsManager.ExtensionPackDirectory.JoinDir(
            PackagePair!.InstalledPackage.PackageName
        );
        if (!extensionPackDir.Exists)
        {
            extensionPackDir.Create();
        }

        if (SelectedExtensionPack is null || ExtensionPacks.Count <= 0)
        {
            await ProcessRunner.OpenFolderBrowser(extensionPackDir);
        }
        else
        {
            var extensionPackPath = extensionPackDir.JoinFile($"{SelectedExtensionPack.Name}.json");
            await ProcessRunner.OpenFileBrowser(extensionPackPath);
        }
    }

    [RelayCommand]
    private async Task SetExtensionVersion(SavedPackageExtension selectedExtension)
    {
        if (SelectedExtensionPack is null)
            return;

        var vm = new GitVersionSelectorViewModel
        {
            GitVersionProvider = new CachedCommandGitVersionProvider(
                selectedExtension.PackageExtension.Reference.ToString(),
                prerequisiteHelper
            ),
        };

        var dialog = vm.GetDialog();

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (string.IsNullOrWhiteSpace(vm.SelectedGitVersion.ToString()))
                return;

            // update the version and save pack
            selectedExtension.Version = new PackageExtensionVersion
            {
                Branch = vm.SelectedGitVersion.Branch,
                CommitSha = vm.SelectedGitVersion.CommitSha,
                Tag = vm.SelectedGitVersion.Tag,
            };
            SaveExtensionPack(SelectedExtensionPack, SelectedExtensionPack.Name);

            await LoadExtensionPacksAsync();
        }
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

    [RelayCommand]
    private void SelectAllInstalledExtensions()
    {
        foreach (var item in InstalledItemsSearchCollection.FilteredItems)
        {
            item.IsSelected = true;
        }
    }

    [RelayCommand]
    private async Task InstallExtensionManualAsync()
    {
        var textField = new TextBoxField
        {
            Label = "Extension URL",
            Validator = text =>
            {
                if (string.IsNullOrWhiteSpace(text))
                    throw new DataValidationException("URL is required");

                if (!Uri.TryCreate(text, UriKind.Absolute, out _))
                    throw new DataValidationException("Invalid URL format");
            },
        };
        var dialog = DialogHelper.CreateTextEntryDialog("Manual Extension Install", "", [textField]);

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        var url = textField.Text.Trim();
        if (string.IsNullOrWhiteSpace(url))
            return;

        // check if have enough parts
        if (url.Split('/').Length < 5)
        {
            notificationService.Show("Invalid URL", "The provided URL does not contain enough information.");
            return;
        }

        // get the author from github url
        var author = url.Split('/')[3];
        // get the title from the url
        var title = url.Split('/')[4].Replace(".git", "");
        // create a new PackageExtension
        var packageExtension = new PackageExtension
        {
            Author = author,
            Title = title,
            Reference = new Uri(url),
            IsInstalled = false,
            InstallType = "git-clone",
            Files = [new Uri(url)],
        };

        var steps = new List<IPackageStep>
        {
            new InstallExtensionStep(
                PackagePair!.BasePackage.ExtensionManager!,
                PackagePair.InstalledPackage,
                packageExtension
            ),
        };

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            ModificationCompleteTitle = "Installed Extensions",
            ModificationCompleteMessage = "Finished installing extensions",
        };
        EventManager.Instance.OnPackageInstallProgressAdded(runner);

        await runner.ExecuteSteps(steps);

        ClearSelection();

        RefreshBackground();
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
            throw new NotSupportedException(
                $"The package {PackagePair?.BasePackage} does not support extensions."
            );

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
            item.IsSelected = false;

        foreach (var item in SelectedInstalledItems.ToImmutableArray())
            item.IsSelected = false;

        foreach (var item in SelectedExtensionPackExtensions.ToImmutableArray())
            item.IsSelected = false;
    }

    private (BetterContentDialog dialog, TextBoxField nameField) GetNameEntryDialog()
    {
        var textFields = new TextBoxField[]
        {
            new()
            {
                Label = "Name",
                Validator = text =>
                {
                    if (string.IsNullOrWhiteSpace(text))
                        throw new DataValidationException("Name is required");

                    if (ExtensionPacks.Any(pack => pack.Name == text))
                        throw new DataValidationException("Pack already exists");
                },
            },
        };

        return (DialogHelper.CreateTextEntryDialog("Pack Name", "", textFields), textFields[0]);
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
                MaxDialogWidth = 400,
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return false;

            settingsManager.Transaction(s =>
                s.SeenTeachingTips.Add(Core.Models.Settings.TeachingTip.PackageExtensionsInstallNotice)
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

        // For available extensions, add installed status if available
        foreach (var (i, ext) in extensions.Enumerate())
            if (extensionsInstalled.Contains(ext))
                extensions[i] = ext with { IsInstalled = true };
    }

    private async Task LoadExtensionPacksAsync()
    {
        if (Design.IsDesignMode)
            return;

        try
        {
            AreExtensionPacksLoading = true;

            var packDir = settingsManager.ExtensionPackDirectory;
            if (!packDir.Exists)
                packDir.Create();

            var jsonFiles = packDir.EnumerateFiles("*.json", SearchOption.AllDirectories);
            var packs = new List<ExtensionPack>();

            foreach (var jsonFile in jsonFiles)
            {
                var json = await jsonFile.ReadAllTextAsync();
                try
                {
                    var extensionPack = JsonSerializer.Deserialize<ExtensionPack>(json);
                    if (
                        extensionPack != null
                        && extensionPack.PackageType == PackagePair!.InstalledPackage.PackageName
                    )
                        packs.Add(extensionPack);
                }
                catch (JsonException)
                {
                    // ignored for now, need to log
                }
            }

            extensionPackSource.AddOrUpdate(packs);
        }
        finally
        {
            AreExtensionPacksLoading = false;
        }
    }

    private void SaveExtensionPack(ExtensionPack newExtensionPack, string name)
    {
        var extensionPackDir = settingsManager.ExtensionPackDirectory.JoinDir(newExtensionPack.PackageType);

        if (!extensionPackDir.Exists)
        {
            extensionPackDir.Create();
        }

        var path = extensionPackDir.JoinFile($"{name}.json");
        var json = JsonSerializer.Serialize(newExtensionPack);
        path.WriteAllText(json);
    }

    partial void OnSelectedExtensionPackChanged(ExtensionPack? value)
    {
        if (value != null)
        {
            extensionPackExtensionsSource.Edit(updater => updater.Load(value.Extensions));
        }
        else
        {
            extensionPackExtensionsSource.Clear();
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
