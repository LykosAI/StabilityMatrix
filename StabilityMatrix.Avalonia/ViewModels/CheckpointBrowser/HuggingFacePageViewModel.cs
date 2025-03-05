using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models.HuggingFace;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.HuggingFacePage;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;

[View(typeof(Views.HuggingFacePage))]
[RegisterSingleton<HuggingFacePageViewModel>]
public partial class HuggingFacePageViewModel : TabViewModelBase
{
    private readonly ITrackedDownloadService trackedDownloadService;
    private readonly ISettingsManager settingsManager;
    private readonly INotificationService notificationService;

    public SourceCache<HuggingfaceItem, string> ItemsCache { get; } =
        new(i => i.RepositoryPath + i.ModelName);

    public IObservableCollection<CategoryViewModel> Categories { get; set; } =
        new ObservableCollectionExtended<CategoryViewModel>();

    public string DownloadPercentText =>
        Math.Abs(TotalProgress.Percentage - 100f) < 0.001f
            ? "Download Complete"
            : $"Downloading {TotalProgress.Percentage:0}%";

    [ObservableProperty]
    private int numSelected;

    private ConcurrentDictionary<Guid, ProgressReport> progressReports = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DownloadPercentText))]
    private ProgressReport totalProgress;

    private readonly DispatcherTimer progressTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };

    public HuggingFacePageViewModel(
        ITrackedDownloadService trackedDownloadService,
        ISettingsManager settingsManager,
        INotificationService notificationService
    )
    {
        this.trackedDownloadService = trackedDownloadService;
        this.settingsManager = settingsManager;
        this.notificationService = notificationService;

        ItemsCache
            .Connect()
            .DeferUntilLoaded()
            .Group(i => i.ModelCategory)
            .Transform(
                g =>
                    new CategoryViewModel(
                        g.Cache.Items,
                        Design.IsDesignMode ? string.Empty : settingsManager.ModelsDirectory
                    )
                    {
                        Title = g.Key.GetDescription() ?? g.Key.ToString()
                    }
            )
            .SortBy(vm => vm.Title ?? "")
            .Bind(Categories)
            .WhenAnyPropertyChanged()
            .Throttle(TimeSpan.FromMilliseconds(50))
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(_ => NumSelected = Categories.Sum(c => c.NumSelected));

        progressTimer.Tick += (_, _) =>
        {
            var currentSum = 0ul;
            var totalSum = 0ul;
            foreach (var progress in progressReports.Values)
            {
                currentSum += progress.Current ?? 0;
                totalSum += progress.Total ?? 0;
            }

            TotalProgress = new ProgressReport(current: currentSum, total: totalSum);
        };
    }

    public override void OnLoaded()
    {
        if (ItemsCache.Count > 0)
            return;

        using var reader = new StreamReader(Assets.HfPackagesJson.Open());
        var packages =
            JsonSerializer.Deserialize<IReadOnlyList<HuggingfaceItem>>(
                reader.ReadToEnd(),
                new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } }
            ) ?? throw new InvalidOperationException("Failed to read hf-packages.json");

        ItemsCache.EditDiff(packages, (a, b) => a.RepositoryPath == b.RepositoryPath);
    }

    public void ClearSelection()
    {
        foreach (var category in Categories)
        {
            category.IsChecked = true;
            category.IsChecked = false;
        }
    }

    public void SelectAll()
    {
        foreach (var category in Categories)
        {
            category.IsChecked = true;
        }
    }

    public void Refresh()
    {
        ItemsCache.Clear();
        OnLoaded();
    }

    [RelayCommand]
    private async Task ImportSelected()
    {
        var selected = Categories.SelectMany(c => c.Items).Where(i => i.IsSelected).ToArray();

        foreach (var viewModel in selected)
        {
            foreach (var file in viewModel.Item.Files)
            {
                var url =
                    $"https://huggingface.co/{viewModel.Item.RepositoryPath}/resolve/main/{file}?download=true";
                var sharedFolderType = viewModel.Item.ModelCategory.ConvertTo<SharedFolderType>();
                var fileName = Path.GetFileName(file);
                var downloadPath = new FilePath(
                    Path.Combine(
                        Design.IsDesignMode ? string.Empty : settingsManager.ModelsDirectory,
                        sharedFolderType.ToString(),
                        viewModel.Item.Subfolder ?? string.Empty,
                        fileName
                    )
                );
                downloadPath.Directory?.Create();
                var download = trackedDownloadService.NewDownload(url, downloadPath);
                download.ProgressUpdate += DownloadOnProgressUpdate;
                download.ProgressStateChanged += (_, e) =>
                {
                    if (e == ProgressState.Success)
                    {
                        viewModel.NotifyExistsChanged();
                    }
                };
                await trackedDownloadService.TryStartDownload(download);

                await Task.Delay(Random.Shared.Next(50, 100));
            }

            viewModel.IsSelected = false;
        }
        progressTimer.Start();
    }

    private void DownloadOnProgressUpdate(object? sender, ProgressReport e)
    {
        if (sender is not TrackedDownload trackedDownload)
            return;

        progressReports[trackedDownload.Id] = e;
    }

    partial void OnTotalProgressChanged(ProgressReport value)
    {
        if (Math.Abs(value.Percentage - 100) < 0.001f)
        {
            notificationService.Show(
                "Download complete",
                "All selected models have been downloaded.",
                NotificationType.Success
            );
            progressTimer.Stop();

            ClearSelection();
            DelayedClearProgress(TimeSpan.FromSeconds(1.5));
        }
    }

    private void DelayedClearProgress(TimeSpan delay)
    {
        Task.Delay(delay)
            .ContinueWith(_ =>
            {
                TotalProgress = new ProgressReport(0, 0);
            });
    }

    public override string Header => Resources.Label_HuggingFace;
}
