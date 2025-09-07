using System;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;
using SymbolIconSource = FluentIcons.Avalonia.Fluent.SymbolIconSource;
using TeachingTip = StabilityMatrix.Core.Models.Settings.TeachingTip;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(ConsoleOutputPage))]
public partial class RunningPackageViewModel : PageViewModelBase, IDisposable, IAsyncDisposable
{
    private readonly ISettingsManager settingsManager;
    private readonly INotificationService notificationService;
    private readonly RunningPackageService runningPackageService;
    private readonly RunPackageOptions runPackageOptions;

    private readonly CompositeDisposable subscriptions = new();

    public PackagePair RunningPackage { get; }
    public ConsoleViewModel Console { get; }
    public override string Title => RunningPackage.InstalledPackage.DisplayName ?? "Running Package";
    public override IconSource IconSource => new SymbolIconSource();

    [ObservableProperty]
    private bool autoScrollToEnd;

    [ObservableProperty]
    private bool showWebUiButton;

    [ObservableProperty]
    private string webUiUrl = string.Empty;

    [ObservableProperty]
    private bool isRunning = true;

    [ObservableProperty]
    private string consoleInput = string.Empty;

    [ObservableProperty]
    private bool showWebUiTeachingTip;

    /// <inheritdoc/>
    public RunningPackageViewModel(
        ISettingsManager settingsManager,
        INotificationService notificationService,
        RunningPackageService runningPackageService,
        PackagePair runningPackage,
        RunPackageOptions runPackageOptions,
        ConsoleViewModel console
    )
    {
        this.settingsManager = settingsManager;
        this.notificationService = notificationService;
        this.runningPackageService = runningPackageService;
        this.runPackageOptions = runPackageOptions;

        RunningPackage = runningPackage;
        Console = console;
        Console.MaxLines = settingsManager.Settings.ConsoleLogHistorySize;
        Console.Document.LineCountChanged += DocumentOnLineCountChanged;
        RunningPackage.BasePackage.StartupComplete += BasePackageOnStartupComplete;
        RunningPackage.BasePackage.Exited += BasePackageOnExited;

        subscriptions.Add(
            settingsManager.RegisterPropertyChangedHandler(
                settings => settings.ConsoleLogHistorySize,
                newValue =>
                {
                    Console.MaxLines = newValue;
                }
            )
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.AutoScrollToEnd,
            settings => settings.AutoScrollLaunchConsoleToEnd,
            true
        );
    }

    public override void OnLoaded()
    {
        if (AutoScrollToEnd)
        {
            EventManager.Instance.OnScrollToBottomRequested();
        }
    }

    private void BasePackageOnExited(object? sender, int e)
    {
        IsRunning = false;
        ShowWebUiButton = false;
        Console.Document.LineCountChanged -= DocumentOnLineCountChanged;
        RunningPackage.BasePackage.StartupComplete -= BasePackageOnStartupComplete;
        RunningPackage.BasePackage.Exited -= BasePackageOnExited;
        runningPackageService.RunningPackages.Remove(RunningPackage.InstalledPackage.Id);
    }

    private void BasePackageOnStartupComplete(object? sender, string url)
    {
        WebUiUrl = url.Replace("0.0.0.0", "127.0.0.1");
        ShowWebUiButton = !string.IsNullOrWhiteSpace(WebUiUrl);

        if (settingsManager.Settings.SeenTeachingTips.Contains(TeachingTip.WebUiButtonMovedTip))
            return;

        ShowWebUiTeachingTip = true;
        settingsManager.Transaction(s => s.SeenTeachingTips.Add(TeachingTip.WebUiButtonMovedTip));
    }

    private void DocumentOnLineCountChanged(object? sender, EventArgs e)
    {
        if (AutoScrollToEnd)
        {
            EventManager.Instance.OnScrollToBottomRequested();
        }
    }

    partial void OnAutoScrollToEndChanged(bool value)
    {
        if (value)
        {
            EventManager.Instance.OnScrollToBottomRequested();
        }
    }

    [RelayCommand]
    private async Task Restart()
    {
        await Stop();
        await Task.Delay(100);
        LaunchPackage();
    }

    [RelayCommand]
    private void LaunchPackage()
    {
        EventManager.Instance.OnPackageRelaunchRequested(RunningPackage.InstalledPackage, runPackageOptions);
    }

    [RelayCommand]
    private async Task Stop()
    {
        IsRunning = false;
        await runningPackageService.StopPackage(RunningPackage.InstalledPackage.Id);
        Console.PostLine($"{Environment.NewLine}Stopped process at {DateTimeOffset.Now}");
        await Console.StopUpdatesAsync();
    }

    [RelayCommand]
    private void LaunchWebUi()
    {
        if (string.IsNullOrEmpty(WebUiUrl))
            return;

        notificationService.TryAsync(
            Task.Run(() => ProcessRunner.OpenUrl(WebUiUrl)),
            "Failed to open URL",
            $"{WebUiUrl}"
        );
    }

    [RelayCommand]
    private async Task SendToConsole()
    {
        Console.PostLine(ConsoleInput);
        if (RunningPackage?.BasePackage is BaseGitPackage gitPackage)
        {
            var venv = gitPackage.VenvRunner;
            var process = venv?.Process;
            if (process is not null)
            {
                await process.StandardInput.WriteLineAsync(ConsoleInput);
            }
        }

        ConsoleInput = string.Empty;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            RunningPackage.BasePackage.Shutdown();
            Console.Dispose();
            subscriptions.Dispose();
        }

        base.Dispose(disposing);
    }

    public async ValueTask DisposeAsync()
    {
        RunningPackage.BasePackage.Shutdown();
        await Console.DisposeAsync();
        subscriptions.Dispose();
        GC.SuppressFinalize(this);
    }
}
