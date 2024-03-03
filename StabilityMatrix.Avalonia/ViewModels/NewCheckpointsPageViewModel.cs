using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;
using Refit;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.CheckpointManager;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Services;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.Avalonia.Fluent.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(NewCheckpointsPage))]
[Singleton]
public partial class NewCheckpointsPageViewModel(
    ILogger<NewCheckpointsPageViewModel> logger,
    ISettingsManager settingsManager,
    ILiteDbContext liteDbContext,
    ICivitApi civitApi,
    ServiceManager<ViewModelBase> dialogFactory,
    INotificationService notificationService,
    IDownloadService downloadService,
    ModelFinder modelFinder,
    IMetadataImportService metadataImportService
) : PageViewModelBase
{
    public override string Title => Resources.Label_CheckpointManager;
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.Cellular5g, IsFilled = true };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectedCheckpoints))]
    [NotifyPropertyChangedFor(nameof(NonConnectedCheckpoints))]
    private ObservableCollection<CheckpointFile> allCheckpoints = new();

    [ObservableProperty]
    private ObservableCollection<CivitModel> civitModels = new();

    public ObservableCollection<CheckpointFile> ConnectedCheckpoints =>
        new(
            AllCheckpoints
                .Where(x => x.IsConnectedModel)
                .OrderBy(x => x.ConnectedModel!.ModelName)
                .ThenBy(x => x.ModelType)
                .GroupBy(x => x.ConnectedModel!.ModelId)
                .Select(x => x.First())
        );

    public ObservableCollection<CheckpointFile> NonConnectedCheckpoints =>
        new(AllCheckpoints.Where(x => !x.IsConnectedModel).OrderBy(x => x.ModelType));

    public override async Task OnLoadedAsync()
    {
        if (Design.IsDesignMode)
            return;

        var files = CheckpointFile.GetAllCheckpointFiles(settingsManager.ModelsDirectory).ToList();

        var uniqueSubFolders = files
            .Select(
                x =>
                    x.FilePath.Replace(settingsManager.ModelsDirectory, string.Empty)
                        .Replace(x.FileName, string.Empty)
                        .Trim(Path.DirectorySeparatorChar)
            )
            .Distinct()
            .Where(x => x.Contains(Path.DirectorySeparatorChar))
            .Where(x => Directory.Exists(Path.Combine(settingsManager.ModelsDirectory, x)))
            .ToList();

        var checkpointFolders = Enum.GetValues<SharedFolderType>()
            .Where(x => Directory.Exists(Path.Combine(settingsManager.ModelsDirectory, x.ToString())))
            .Select(
                folderType =>
                    new CheckpointFolder(
                        settingsManager,
                        downloadService,
                        modelFinder,
                        notificationService,
                        metadataImportService
                    )
                    {
                        Title = folderType.ToString(),
                        DirectoryPath = Path.Combine(settingsManager.ModelsDirectory, folderType.ToString()),
                        FolderType = folderType,
                        IsExpanded = true,
                    }
            )
            .ToList();

        foreach (var folder in uniqueSubFolders)
        {
            var folderType = Enum.Parse<SharedFolderType>(folder.Split(Path.DirectorySeparatorChar)[0]);
            var parentFolder = checkpointFolders.FirstOrDefault(x => x.FolderType == folderType);
            var checkpointFolder = new CheckpointFolder(
                settingsManager,
                downloadService,
                modelFinder,
                notificationService,
                metadataImportService
            )
            {
                Title = folderType.ToString(),
                DirectoryPath = Path.Combine(settingsManager.ModelsDirectory, folder),
                FolderType = folderType,
                ParentFolder = parentFolder,
                IsExpanded = true,
            };
            parentFolder?.SubFolders.Add(checkpointFolder);
        }

        AllCheckpoints = new ObservableCollection<CheckpointFile>(files);

        var connectedModelIds = ConnectedCheckpoints.Select(x => x.ConnectedModel.ModelId);
        var modelRequest = new CivitModelsRequest
        {
            CommaSeparatedModelIds = string.Join(',', connectedModelIds)
        };

        // See if query is cached
        var cachedQuery = await liteDbContext
            .CivitModelQueryCache.IncludeAll()
            .FindByIdAsync(ObjectHash.GetMd5Guid(modelRequest));

        // If cached, update model cards
        if (cachedQuery is not null)
        {
            CivitModels = new ObservableCollection<CivitModel>(cachedQuery.Items);

            // Start remote query (background mode)
            // Skip when last query was less than 2 min ago
            var timeSinceCache = DateTimeOffset.UtcNow - cachedQuery.InsertedAt;
            if (timeSinceCache?.TotalMinutes >= 2)
            {
                CivitQuery(modelRequest).SafeFireAndForget();
            }
        }
        else
        {
            await CivitQuery(modelRequest);
        }
    }

    public async Task ShowVersionDialog(int modelId)
    {
        var model = CivitModels.FirstOrDefault(m => m.Id == modelId);
        if (model == null)
        {
            notificationService.Show(
                new Notification(
                    "Model has no versions available",
                    "This model has no versions available for download",
                    NotificationType.Warning
                )
            );
            return;
        }
        var versions = model.ModelVersions;
        if (versions is null || versions.Count == 0)
        {
            notificationService.Show(
                new Notification(
                    "Model has no versions available",
                    "This model has no versions available for download",
                    NotificationType.Warning
                )
            );
            return;
        }

        var dialog = new BetterContentDialog
        {
            Title = model.Name,
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            IsFooterVisible = false,
            MaxDialogWidth = 750,
        };

        var viewModel = dialogFactory.Get<SelectModelVersionViewModel>();
        viewModel.Dialog = dialog;
        viewModel.Versions = versions
            .Select(
                version =>
                    new ModelVersionViewModel(
                        settingsManager.Settings.InstalledModelHashes ?? new HashSet<string>(),
                        version
                    )
            )
            .ToImmutableArray();
        viewModel.SelectedVersionViewModel = viewModel.Versions[0];

        dialog.Content = new SelectModelVersionDialog { DataContext = viewModel };

        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var selectedVersion = viewModel?.SelectedVersionViewModel?.ModelVersion;
        var selectedFile = viewModel?.SelectedFile?.CivitFile;
    }

    private async Task CivitQuery(CivitModelsRequest request)
    {
        try
        {
            var modelResponse = await civitApi.GetModels(request);
            var models = modelResponse.Items;
            // Filter out unknown model types and archived/taken-down models
            models = models
                .Where(m => m.Type.ConvertTo<SharedFolderType>() > 0)
                .Where(m => m.Mode == null)
                .ToList();

            // Database update calls will invoke `OnModelsUpdated`
            // Add to database
            await liteDbContext.UpsertCivitModelAsync(models);
            // Add as cache entry
            var cacheNew = await liteDbContext.UpsertCivitModelQueryCacheEntryAsync(
                new CivitModelQueryCacheEntry
                {
                    Id = ObjectHash.GetMd5Guid(request),
                    InsertedAt = DateTimeOffset.UtcNow,
                    Request = request,
                    Items = models,
                    Metadata = modelResponse.Metadata
                }
            );

            if (cacheNew)
            {
                CivitModels = new ObservableCollection<CivitModel>(models);
            }
        }
        catch (OperationCanceledException)
        {
            notificationService.Show(
                new Notification(
                    "Request to CivitAI timed out",
                    "Could not check for checkpoint updates. Please try again later."
                )
            );
            logger.LogWarning($"CivitAI query timed out ({request})");
        }
        catch (HttpRequestException e)
        {
            notificationService.Show(
                new Notification(
                    "CivitAI can't be reached right now",
                    "Could not check for checkpoint updates. Please try again later."
                )
            );
            logger.LogWarning(e, $"CivitAI query HttpRequestException ({request})");
        }
        catch (ApiException e)
        {
            notificationService.Show(
                new Notification(
                    "CivitAI can't be reached right now",
                    "Could not check for checkpoint updates. Please try again later."
                )
            );
            logger.LogWarning(e, $"CivitAI query ApiException ({request})");
        }
        catch (Exception e)
        {
            notificationService.Show(
                new Notification(
                    "CivitAI can't be reached right now",
                    $"Unknown exception during CivitAI query: {e.GetType().Name}"
                )
            );
            logger.LogError(e, $"CivitAI query unknown exception ({request})");
        }
    }
}
