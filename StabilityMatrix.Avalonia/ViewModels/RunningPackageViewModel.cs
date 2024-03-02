using System;
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

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(ConsoleOutputPage))]
public partial class RunningPackageViewModel : PageViewModelBase, IDisposable, IAsyncDisposable
{
    private readonly INotificationService notificationService;
    private readonly RunningPackageService runningPackageService;

    public PackagePair RunningPackage { get; }
    public ConsoleViewModel Console { get; }
    public override string Title => RunningPackage.InstalledPackage.PackageName ?? "Running Package";
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

    /// <inheritdoc/>
    public RunningPackageViewModel(
        ISettingsManager settingsManager,
        INotificationService notificationService,
        RunningPackageService runningPackageService,
        PackagePair runningPackage,
        ConsoleViewModel console
    )
    {
        this.notificationService = notificationService;
        this.runningPackageService = runningPackageService;

        RunningPackage = runningPackage;
        Console = console;
        Console.Document.LineCountChanged += DocumentOnLineCountChanged;
        RunningPackage.BasePackage.StartupComplete += BasePackageOnStartupComplete;
        RunningPackage.BasePackage.Exited += BasePackageOnExited;

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.AutoScrollToEnd,
            settings => settings.AutoScrollLaunchConsoleToEnd,
            true
        );
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
    }

    private void DocumentOnLineCountChanged(object? sender, EventArgs e)
    {
        if (AutoScrollToEnd)
        {
            EventManager.Instance.OnScrollToBottomRequested();
        }
    }

    [RelayCommand]
    private void LaunchPackage()
    {
        EventManager.Instance.OnPackageRelaunchRequested(RunningPackage.InstalledPackage);
    }

    [RelayCommand]
    private async Task Stop()
    {
        await runningPackageService.StopPackage(RunningPackage.InstalledPackage.Id);
        Console.PostLine($"{Environment.NewLine}Stopped process at {DateTimeOffset.Now}");
        await Console.StopUpdatesAsync();
        IsRunning = false;
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

    public void Dispose()
    {
        Console.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await Console.DisposeAsync();
    }
}
