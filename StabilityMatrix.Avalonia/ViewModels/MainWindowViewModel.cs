using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using NLog;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.ViewModels.Progress;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Analytics;
using StabilityMatrix.Core.Models.Update;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(MainWindow))]
public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly ISettingsManager settingsManager;
    private readonly ServiceManager<ViewModelBase> dialogFactory;
    private readonly ITrackedDownloadService trackedDownloadService;
    private readonly IDiscordRichPresenceService discordRichPresenceService;
    private readonly IModelIndexService modelIndexService;
    private readonly Lazy<IModelDownloadLinkHandler> modelDownloadLinkHandler;
    private readonly INotificationService notificationService;
    private readonly IAnalyticsHelper analyticsHelper;
    public string Greeting => "Welcome to Avalonia!";

    [ObservableProperty]
    private PageViewModelBase? currentPage;

    [ObservableProperty]
    private object? selectedCategory;

    [ObservableProperty]
    private List<PageViewModelBase> pages = new();

    [ObservableProperty]
    private List<PageViewModelBase> footerPages = new();

    public ProgressManagerViewModel ProgressManagerViewModel { get; init; }
    public UpdateViewModel UpdateViewModel { get; init; }

    public double PaneWidth =>
        Cultures.Current switch
        {
            { Name: "it-IT" } => 250,
            { Name: "fr-FR" } => 250,
            { Name: "es" } => 250,
            { Name: "ru-RU" } => 250,
            { Name: "tr-TR" } => 235,
            { Name: "de" } => 250,
            { Name: "pt-PT" } => 300,
            { Name: "pt-BR" } => 260,
            _ => 200
        };

    public MainWindowViewModel(
        ISettingsManager settingsManager,
        IDiscordRichPresenceService discordRichPresenceService,
        ServiceManager<ViewModelBase> dialogFactory,
        ITrackedDownloadService trackedDownloadService,
        IModelIndexService modelIndexService,
        Lazy<IModelDownloadLinkHandler> modelDownloadLinkHandler,
        INotificationService notificationService,
        IAnalyticsHelper analyticsHelper
    )
    {
        this.settingsManager = settingsManager;
        this.dialogFactory = dialogFactory;
        this.discordRichPresenceService = discordRichPresenceService;
        this.trackedDownloadService = trackedDownloadService;
        this.modelIndexService = modelIndexService;
        this.modelDownloadLinkHandler = modelDownloadLinkHandler;
        this.notificationService = notificationService;
        this.analyticsHelper = analyticsHelper;
        ProgressManagerViewModel = dialogFactory.Get<ProgressManagerViewModel>();
        UpdateViewModel = dialogFactory.Get<UpdateViewModel>();
    }

    public override void OnLoaded()
    {
        base.OnLoaded();

        // Set only if null, since this may be called again when content dialogs open
        CurrentPage ??= Pages.FirstOrDefault();
        SelectedCategory ??= Pages.FirstOrDefault();
    }

    protected override async Task OnInitialLoadedAsync()
    {
        await base.OnLoadedAsync();

        // Skip if design mode
        if (Design.IsDesignMode)
            return;

        if (!await EnsureDataDirectory())
        {
            // False if user exited dialog, shutdown app
            App.Shutdown();
            return;
        }

        Task.Run(() => SharedFolders.SetupSharedModelFolders(settingsManager.ModelsDirectory))
            .SafeFireAndForget(ex =>
            {
                Logger.Error(ex, "Error setting up shared model folders");
            });

        try
        {
            await modelDownloadLinkHandler.Value.StartListening();
        }
        catch (IOException)
        {
            var dialog = new BetterContentDialog
            {
                Title = Resources.Label_StabilityMatrixAlreadyRunning,
                Content = Resources.Label_AnotherInstanceAlreadyRunning,
                IsPrimaryButtonEnabled = true,
                PrimaryButtonText = Resources.Action_Close,
                DefaultButton = ContentDialogButton.Primary
            };
            await dialog.ShowAsync();
            App.Shutdown();
            return;
        }

        // Initialize Discord Rich Presence (this needs LibraryDir so is set here)
        discordRichPresenceService.UpdateState();

        // Load in-progress downloads
        ProgressManagerViewModel.AddDownloads(trackedDownloadService.Downloads);

        // Index checkpoints if we dont have
        // Task.Run(() => settingsManager.IndexCheckpoints()).SafeFireAndForget();

        // Disable preload for now, might be causing https://github.com/LykosAI/StabilityMatrix/issues/249
        /*if (!App.IsHeadlessMode)
        {
            PreloadPages();
        }*/

        Program.StartupTimer.Stop();
        var startupTime = CodeTimer.FormatTime(Program.StartupTimer.Elapsed);
        Logger.Info($"App started ({startupTime})");

        // Show analytics notice if not seen
        if (
            !settingsManager.Settings.SeenTeachingTips.Contains(
                Core.Models.Settings.TeachingTip.PackageInstallAnalyticsOptIn
            )
        )
        {
            var vm = dialogFactory.Get<AnalyticsOptInViewModel>();
            var result = await vm.GetDialog().ShowAsync();

            if (result == ContentDialogResult.Secondary)
            {
                settingsManager.Transaction(s => s.OptedInToInstallTelemetry = true);
            }

            settingsManager.Transaction(
                s => s.SeenTeachingTips.Add(Core.Models.Settings.TeachingTip.PackageInstallAnalyticsOptIn)
            );
        }

        if (Program.Args.DebugOneClickInstall || settingsManager.Settings.InstalledPackages.Count == 0)
        {
            var viewModel = dialogFactory.Get<NewOneClickInstallViewModel>();
            var dialog = new BetterContentDialog
            {
                IsPrimaryButtonEnabled = false,
                IsSecondaryButtonEnabled = false,
                IsFooterVisible = false,
                FullSizeDesired = true,
                MinDialogHeight = 775,
                Content = new NewOneClickInstallDialog { DataContext = viewModel },
            };

            var firstDialogResult = await dialog.ShowAsync(App.TopLevel);

            if (firstDialogResult != ContentDialogResult.Primary)
                return;

            var recommendedModelsViewModel = dialogFactory.Get<RecommendedModelsViewModel>();
            dialog = new BetterContentDialog
            {
                IsPrimaryButtonEnabled = true,
                FullSizeDesired = true,
                MinDialogHeight = 900,
                PrimaryButtonText = Resources.Action_Download,
                CloseButtonText = Resources.Action_Close,
                DefaultButton = ContentDialogButton.Primary,
                PrimaryButtonCommand = recommendedModelsViewModel.DoImportCommand,
                Content = new RecommendedModelsDialog { DataContext = recommendedModelsViewModel },
            };

            await dialog.ShowAsync(App.TopLevel);

            EventManager.Instance.OnRecommendedModelsDialogClosed();
            EventManager.Instance.OnDownloadsTeachingTipRequested();

            var installedPackageNameMaybe =
                settingsManager.PackageInstallsInProgress.FirstOrDefault()
                ?? settingsManager.Settings.InstalledPackages.FirstOrDefault()?.PackageName;

            analyticsHelper
                .TrackFirstTimeInstallAsync(
                    installedPackageNameMaybe,
                    recommendedModelsViewModel
                        .Sd15Models.Concat(recommendedModelsViewModel.SdxlModels)
                        .Select(x => x.CivitModel.Name)
                        .ToList(),
                    false,
                    Compat.Platform.ToString()
                )
                .SafeFireAndForget();
        }

        // Show what's new for updates
        if (settingsManager.Settings.UpdatingFromVersion is { } updatingFromVersion)
        {
            var currentVersion = Compat.AppVersion;

            notificationService.Show(
                "Update Successful",
                $"Stability Matrix has been updated from {updatingFromVersion.ToDisplayString()} to {currentVersion.ToDisplayString()}."
            );

            settingsManager.Transaction(s => s.UpdatingFromVersion = null);
        }
    }

    private void PreloadPages()
    {
        // Preload pages with Preload attribute
        foreach (
            var page in Pages
                .Concat(FooterPages)
                .Where(p => p.GetType().GetCustomAttributes(typeof(PreloadAttribute), true).Any())
        )
        {
            Dispatcher
                .UIThread.InvokeAsync(
                    async () =>
                    {
                        var stopwatch = Stopwatch.StartNew();

                        // ReSharper disable once MethodHasAsyncOverload
                        page.OnLoaded();
                        await page.OnLoadedAsync();

                        // Get view
                        new ViewLocator().Build(page);

                        Logger.Trace(
                            $"Preloaded page {page.GetType().Name} in {stopwatch.Elapsed.TotalMilliseconds:F1}ms"
                        );
                    },
                    DispatcherPriority.Background
                )
                .ContinueWith(task =>
                {
                    if (task.Exception is { } exception)
                    {
                        Logger.Error(exception, "Error preloading page");
                        Debug.Fail(exception.Message);
                    }
                });
        }
    }

    /// <summary>
    /// Check if the data directory exists, if not, show the select data directory dialog.
    /// </summary>
    private async Task<bool> EnsureDataDirectory()
    {
        // If we can't find library, show selection dialog
        var foundInitially = settingsManager.TryFindLibrary();
        if (!foundInitially)
        {
            var result = await ShowSelectDataDirectoryDialog();
            if (!result)
                return false;
        }

        // Try to find library again, should be found now
        if (!settingsManager.TryFindLibrary())
        {
            throw new Exception("Could not find library after setting path");
        }

        // Tell LaunchPage to load any packages if they selected an existing directory
        if (!foundInitially)
        {
            EventManager.Instance.OnInstalledPackagesChanged();
        }

        // Check if there are old packages, if so show migration dialog
        // TODO: Migration dialog

        return true;
    }

    /// <summary>
    /// Return true if we should show the update available teaching tip
    /// </summary>
    public bool ShouldShowUpdateAvailableTeachingTip([NotNullWhen(true)] UpdateInfo? info)
    {
        if (info is null)
        {
            return false;
        }

        // If matching settings seen version, don't show
        if (info.Version == settingsManager.Settings.LastSeenUpdateVersion)
        {
            return false;
        }

        // Save that we have dismissed this update
        settingsManager.Transaction(
            s => s.LastSeenUpdateVersion = info.Version,
            ignoreMissingLibraryDir: true
        );

        return true;
    }

    /// <summary>
    /// Shows the select data directory dialog.
    /// </summary>
    /// <returns>true if path set successfully, false if user exited dialog.</returns>
    private async Task<bool> ShowSelectDataDirectoryDialog()
    {
        var viewModel = dialogFactory.Get<SelectDataDirectoryViewModel>();
        var dialog = new BetterContentDialog
        {
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            IsFooterVisible = false,
            Content = new SelectDataDirectoryDialog { DataContext = viewModel }
        };

        var result = await dialog.ShowAsync(App.TopLevel);
        if (result == ContentDialogResult.Primary)
        {
            // 1. For portable mode, call settings.SetPortableMode()
            if (viewModel.IsPortableMode)
            {
                settingsManager.SetPortableMode();
            }
            // 2. For custom path, call settings.SetLibraryPath(path)
            else
            {
                settingsManager.SetLibraryPath(viewModel.DataDirectory);
            }
            // Indicate success
            return true;
        }

        return false;
    }

    public async Task ShowUpdateDialog()
    {
        var viewModel = dialogFactory.Get<UpdateViewModel>();
        var dialog = new BetterContentDialog
        {
            ContentVerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            IsFooterVisible = false,
            Content = new UpdateDialog { DataContext = viewModel }
        };

        await viewModel.Preload();
        await dialog.ShowAsync();
    }
}
