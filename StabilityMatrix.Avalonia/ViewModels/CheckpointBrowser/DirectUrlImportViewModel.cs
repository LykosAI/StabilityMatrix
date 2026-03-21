using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using StabilityMatrix.Avalonia;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;

[View(typeof(Views.DirectUrlImportPage))]
[RegisterSingleton<DirectUrlImportViewModel>]
public partial class DirectUrlImportViewModel : TabViewModelBase
{
    private const string CustomLocationLabel = "Custom...";
    private const string CivitAiHost = "civitai.com";
    private const string WwwCivitAiHost = "www.civitai.com";
    private const int DefaultCivitPageLimit = 30;
    private static readonly string[] UrlLineSeparators = ["\r\n", "\r", "\n"];

    private readonly IModelImportService modelImportService;
    private readonly CivitCompatApiManager civitApi;
    private readonly ISettingsManager settingsManager;
    private readonly INotificationService notificationService;

    private readonly Queue<CivitAiImportContext> civitAiImportQueue = new();
    private CivitAiImportContext? activeCivitAiContext;
    private DirectoryPath activeDownloadFolder;

    [ObservableProperty]
    private string urlsInput = string.Empty;

    [ObservableProperty]
    private string modelFileName = string.Empty;

    [ObservableProperty]
    private string selectedDownloadLocation = string.Empty;

    [ObservableProperty]
    private List<string> availableDownloadLocations = new();

    [ObservableProperty]
    private string customDownloadLocation = string.Empty;

    [ObservableProperty]
    private bool isImporting;

    [ObservableProperty]
    private string importStatus = string.Empty;

    [ObservableProperty]
    private bool isCivitAiSelectionMode;

    [ObservableProperty]
    private string civitAiCurrentPageLabel = string.Empty;

    [ObservableProperty]
    private string civitAiCurrentPageUrl = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CivitModelSelectionItem> civitAiCurrentPageModels = new();

    [ObservableProperty]
    private bool hasMoreCivitAiPages;

    [ObservableProperty]
    private string civitAiSelectionStatus = string.Empty;

    private string lastValidDownloadLocation = string.Empty;

    public override string Header => "Direct URL";

    public DirectUrlImportViewModel(
        IModelImportService modelImportService,
        CivitCompatApiManager civitApi,
        ISettingsManager settingsManager,
        INotificationService notificationService
    )
    {
        this.modelImportService = modelImportService;
        this.civitApi = civitApi;
        this.settingsManager = settingsManager;
        this.notificationService = notificationService;
    }

    public override void OnLoaded()
    {
        LoadAvailableDownloadLocations();
        if (AvailableDownloadLocations.Count > 0)
        {
            SelectedDownloadLocation = AvailableDownloadLocations[0];
            if (!IsCustomLocation(SelectedDownloadLocation))
            {
                lastValidDownloadLocation = SelectedDownloadLocation;
            }
        }
    }

    private static bool IsCustomLocation(string value)
    {
        return string.Equals(value, CustomLocationLabel, StringComparison.OrdinalIgnoreCase);
    }

    partial void OnSelectedDownloadLocationChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (IsCustomLocation(value))
        {
            if (string.IsNullOrWhiteSpace(CustomDownloadLocation))
            {
                SelectCustomFolderCommand.Execute(null);
            }

            return;
        }

        lastValidDownloadLocation = value;
    }

    private void LoadAvailableDownloadLocations()
    {
        var locations = new List<string>();
        var modelsDir = new DirectoryPath(settingsManager.ModelsDirectory);

        // Add standard shared folder types
        foreach (var folderType in Enum.GetValues(typeof(SharedFolderType)).Cast<SharedFolderType>())
        {
            if (folderType == SharedFolderType.Unknown)
            {
                continue;
            }

            var path = modelsDir.JoinDir(folderType.GetStringValue()).ToString();
            var displayName = $"Models/{folderType.GetStringValue()}";
            locations.Add(displayName);
        }

        // Add custom location option
        locations.Add(CustomLocationLabel);

        AvailableDownloadLocations = locations;
    }

    [RelayCommand]
    private async Task Import()
    {
        if (string.IsNullOrWhiteSpace(UrlsInput))
        {
            notificationService.Show(
                new Notification(
                    "No URLs provided",
                    "Please enter at least one URL",
                    NotificationType.Warning
                )
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedDownloadLocation))
        {
            notificationService.Show(
                new Notification(
                    "No location selected",
                    "Please select a download location",
                    NotificationType.Warning
                )
            );
            return;
        }

        if (IsCustomLocation(SelectedDownloadLocation) && string.IsNullOrWhiteSpace(CustomDownloadLocation))
        {
            notificationService.Show(
                new Notification(
                    "No custom location selected",
                    "Please pick a folder for the custom location",
                    NotificationType.Warning
                )
            );
            return;
        }

        try
        {
            IsImporting = true;
            ImportStatus = "Parsing URLs...";

            var parsedUrls = ParseUrls(UrlsInput);
            if (parsedUrls.Count == 0)
            {
                notificationService.Show(
                    new Notification(
                        "Invalid URLs",
                        "Please enter at least one valid URL",
                        NotificationType.Warning
                    )
                );
                return;
            }

            var directUrls = new List<Uri>();
            var civitAiContexts = new List<CivitAiImportContext>();

            foreach (var parsedUrl in parsedUrls)
            {
                if (TryCreateCivitAiImportContext(parsedUrl, out var civitAiContext))
                {
                    civitAiContexts.Add(civitAiContext);
                }
                else
                {
                    directUrls.Add(parsedUrl);
                }
            }

            if (directUrls.Count > 0 && string.IsNullOrWhiteSpace(ModelFileName))
            {
                notificationService.Show(
                    new Notification(
                        "No filename provided",
                        "Please enter a filename for fallback direct downloads",
                        NotificationType.Warning
                    )
                );
                return;
            }

            var downloadFolder = ResolveDownloadFolder();
            activeDownloadFolder = downloadFolder;

            if (directUrls.Count > 0)
            {
                ImportStatus = $"Downloading from {directUrls.Count} direct URL(s)...";
                await modelImportService.DoCustomImport(
                    directUrls,
                    ModelFileName,
                    downloadFolder
                );

                ImportStatus = $"Started download of {ModelFileName}";
                notificationService.Show(
                    new Notification(
                        "Download started",
                        $"Attempting to download {ModelFileName} from {directUrls.Count} URL(s)",
                        NotificationType.Success
                    )
                );
            }

            if (civitAiContexts.Count > 0)
            {
                StartCivitAiSelectionFlow(civitAiContexts);
                ImportStatus = "Loading CivitAI models...";
                await LoadCurrentCivitAiPageAsync();
                return;
            }

            // Clear input after successful direct import
            UrlsInput = string.Empty;
            ModelFileName = string.Empty;
            ImportStatus = string.Empty;
        }
        catch (UriFormatException ex)
        {
            notificationService.Show(
                new Notification(
                    "Invalid URL",
                    $"One or more URLs are invalid: {ex.Message}",
                    NotificationType.Error
                )
            );
        }
        catch (Exception ex)
        {
            notificationService.Show(
                new Notification(
                    "Import failed",
                    $"Error: {ex.Message}",
                    NotificationType.Error
                )
            );
        }
        finally
        {
            if (!IsCivitAiSelectionMode)
            {
                IsImporting = false;
            }
        }
    }

    private static List<Uri> ParseUrls(string rawInput)
    {
        var urls = new List<Uri>();

        foreach (var line in rawInput.Split(UrlLineSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed))
            {
                throw new UriFormatException($"Invalid URL: {trimmed}");
            }

            urls.Add(parsed);
        }

        return urls;
    }

    private static string[] ParseListValues(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return [];
        }

        return rawValue.Split([
            ',',
            ';'
        ], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string? GetQueryValue(Uri uri, string key)
    {
        if (string.IsNullOrWhiteSpace(uri.Query) || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var query = uri.Query.AsSpan();
        if (query.Length <= 1)
        {
            return null;
        }

        if (query[0] == '?')
        {
            query = query[1..];
        }

        foreach (var pair in query.ToString().Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var rawName = parts.Length > 0 ? WebUtility.UrlDecode(parts[0]) : null;
            if (!string.Equals(rawName, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (parts.Length == 1)
            {
                return string.Empty;
            }

            return WebUtility.UrlDecode(parts[1]);
        }

        return null;
    }

    private static int? ParseQueryInt(string? rawValue)
    {
        return int.TryParse(rawValue, out var value) ? value : null;
    }

    private static TEnum? TryParseEnum<TEnum>(string? value)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse(value, true, out TEnum parsed)
            ? parsed
            : null;
    }

    private static CivitModelsRequest CloneRequest(CivitModelsRequest request)
    {
        return new CivitModelsRequest
        {
            Limit = request.Limit,
            Page = request.Page,
            Query = request.Query,
            Tag = request.Tag,
            Username = request.Username,
            Types = request.Types?.ToArray(),
            Sort = request.Sort,
            Period = request.Period,
            Rating = request.Rating,
            Favorites = request.Favorites,
            Hidden = request.Hidden,
            PrimaryFileOnly = request.PrimaryFileOnly,
            AllowDerivatives = request.AllowDerivatives,
            AllowDifferentLicenses = request.AllowDifferentLicenses,
            AllowCommercialUse = request.AllowCommercialUse,
            Nsfw = request.Nsfw,
            BaseModels = request.BaseModels?.ToArray(),
            CommaSeparatedModelIds = request.CommaSeparatedModelIds,
            Cursor = request.Cursor,
        };
    }

    private DirectoryPath ResolveDownloadFolder()
    {
        if (IsCustomLocation(SelectedDownloadLocation))
        {
            return new DirectoryPath(CustomDownloadLocation);
        }

        var folderName = SelectedDownloadLocation.Split('/').Last();
        return new DirectoryPath(settingsManager.ModelsDirectory, folderName);
    }

    private static bool IsCivitAiHost(Uri uri)
    {
        return uri.Host.Equals(CivitAiHost, StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals(WwwCivitAiHost, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCivitAiModelsUrl(Uri uri)
    {
        return uri.AbsolutePath.StartsWith("/models", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryCreateCivitAiImportContext(Uri uri, out CivitAiImportContext? context)
    {
        context = null;

        if (!IsCivitAiHost(uri) || !IsCivitAiModelsUrl(uri))
        {
            return false;
        }

        var request = new CivitModelsRequest { Limit = DefaultCivitPageLimit, Nsfw = "true" };

        var pathParts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pathParts.Length > 1 && int.TryParse(pathParts[1], out var modelId))
        {
            request.CommaSeparatedModelIds = modelId.ToString();
            context = new CivitAiImportContext
            {
                Request = request,
                SourceUrl = uri,
                SourceLabel = $"https://{CivitAiHost}/models/{modelId}",
            };

            return true;
        }

        var queryValue = GetQueryValue(uri, "query");
        var tagValue = GetQueryValue(uri, "tag");
        var usernameValue = GetQueryValue(uri, "username");
        var idsValue = GetQueryValue(uri, "ids");
        var modelIdValue = GetQueryValue(uri, "modelId");
        var sortValue = GetQueryValue(uri, "sort");
        var periodValue = GetQueryValue(uri, "period");
        var cursorValue = GetQueryValue(uri, "cursor");
        var pageValue = GetQueryValue(uri, "page");
        var limitValue = GetQueryValue(uri, "limit");
        var typesValue = GetQueryValue(uri, "types");
        var baseModelsValue = GetQueryValue(uri, "baseModels");

        if (!string.IsNullOrWhiteSpace(queryValue))
        {
            request.Query = queryValue;
        }

        if (!string.IsNullOrWhiteSpace(tagValue))
        {
            request.Tag = tagValue;
        }

        if (!string.IsNullOrWhiteSpace(usernameValue))
        {
            request.Username = usernameValue;
        }

        if (!string.IsNullOrWhiteSpace(idsValue))
        {
            request.CommaSeparatedModelIds = idsValue;
        }

        if (!string.IsNullOrWhiteSpace(modelIdValue))
        {
            request.CommaSeparatedModelIds = modelIdValue;
        }

        var sort = TryParseEnum<CivitSortMode>(sortValue);
        if (sort is not null)
        {
            request.Sort = sort;
        }

        var period = TryParseEnum<CivitPeriod>(periodValue);
        if (period is not null)
        {
            request.Period = period;
        }

        if (!string.IsNullOrWhiteSpace(cursorValue))
        {
            request.Cursor = cursorValue;
        }

        var page = ParseQueryInt(pageValue);
        if (page is not null)
        {
            request.Page = page;
        }

        var limit = ParseQueryInt(limitValue);
        if (limit is > 0)
        {
            request.Limit = Math.Min(limit.Value, 200);
        }

        var typeStrings = ParseListValues(typesValue);
        if (typeStrings.Length > 0)
        {
            var types = new List<CivitModelType>();
            foreach (var type in typeStrings)
            {
                var parsedType = TryParseEnum<CivitModelType>(type);
                if (parsedType is not null)
                {
                    types.Add(parsedType.Value);
                }
            }

            request.Types = types.Count > 0 ? [.. types] : null;
        }

        var baseModels = ParseListValues(baseModelsValue);
        request.BaseModels = baseModels.Length > 0 ? baseModels : null;

        context = new CivitAiImportContext
        {
            Request = request,
            SourceUrl = uri,
            SourceLabel = uri.ToString(),
        };

        return true;
    }

    private void StartCivitAiSelectionFlow(IReadOnlyList<CivitAiImportContext> civitAiContexts)
    {
        civitAiImportQueue.Clear();

        foreach (var context in civitAiContexts)
        {
            civitAiImportQueue.Enqueue(context);
        }

        if (!civitAiImportQueue.TryDequeue(out var firstContext))
        {
            return;
        }

        activeCivitAiContext = firstContext;
        IsCivitAiSelectionMode = true;
        CivitAiSelectionStatus = string.Empty;
        CivitAiCurrentPageUrl = firstContext.SourceLabel;
    }

    [RelayCommand]
    private async Task ImportSelectedCivitAiModels()
    {
        var selected = CivitAiCurrentPageModels.Where(x => x.IsSelected).ToList();
        if (selected.Count == 0)
        {
            notificationService.Show(
                new Notification(
                    "No models selected",
                    "Please select at least one model from this page",
                    NotificationType.Warning
                )
            );
            return;
        }

        IsImporting = true;

        try
        {
            var started = 0;
            var failed = 0;

            foreach (var item in selected)
            {
                if (await TryImportCivitModel(item.Model, item.ModelVersion))
                {
                    started++;
                }
                else
                {
                    failed++;
                }
            }

            if (failed > 0)
            {
                notificationService.Show(
                    new Notification(
                        "Some imports failed",
                        $"Started {started} model(s), skipped {failed} model(s)",
                        NotificationType.Warning
                    )
                );
            }
            else
            {
                notificationService.Show(
                    new Notification(
                        "Imports started",
                        $"Started {started} model(s)",
                        NotificationType.Success
                    )
                );
            }

            await MoveToNextCivitAiPageAsync();
        }
        finally
        {
            IsImporting = false;
        }
    }

    [RelayCommand]
    private async Task SkipCurrentCivitAiPage()
    {
        if (IsImporting)
        {
            return;
        }

        await MoveToNextCivitAiPageAsync();
    }

    [RelayCommand]
    private void CancelCivitAiSelection()
    {
        ResetCivitAiSelectionFlow();
        IsImporting = false;
        ImportStatus = string.Empty;
    }

    [RelayCommand]
    private async Task ContinueNextCivitAiPage()
    {
        await MoveToNextCivitAiPageAsync();
    }

    private async Task MoveToNextCivitAiPageAsync()
    {
        if (activeCivitAiContext is null)
        {
            ResetCivitAiSelectionFlow();
            return;
        }

        if (!string.IsNullOrWhiteSpace(activeCivitAiContext.NextCursor))
        {
            activeCivitAiContext.CurrentCursor = activeCivitAiContext.NextCursor;
            activeCivitAiContext.NextCursor = null;
            await LoadCurrentCivitAiPageAsync();
            return;
        }

        if (activeCivitAiContext.NextPage is > 0)
        {
            activeCivitAiContext.CurrentCursor = null;
            activeCivitAiContext.Request.Page = activeCivitAiContext.NextPage;
            activeCivitAiContext.Request.Cursor = null;
            activeCivitAiContext.NextPage = null;
            await LoadCurrentCivitAiPageAsync();
            return;
        }

        if (!civitAiImportQueue.TryDequeue(out var nextContext))
        {
            ResetCivitAiSelectionFlow();
            UrlsInput = string.Empty;
            ModelFileName = string.Empty;
            ImportStatus = string.Empty;
            return;
        }

        activeCivitAiContext = nextContext;
        CivitAiCurrentPageUrl = nextContext.SourceLabel;
        await LoadCurrentCivitAiPageAsync();
    }

    private async Task LoadCurrentCivitAiPageAsync()
    {
        if (activeCivitAiContext is null)
        {
            ResetCivitAiSelectionFlow();
            return;
        }

        IsImporting = true;

        try
        {
            var request = CloneRequest(activeCivitAiContext.Request);
            if (!string.IsNullOrWhiteSpace(activeCivitAiContext.CurrentCursor))
            {
                request.Cursor = activeCivitAiContext.CurrentCursor;
            }

            var response = await civitApi.GetModels(request);

            CivitAiCurrentPageModels.Clear();

            if (response.Items is not { Count: > 0 })
            {
                CivitAiSelectionStatus = "No models found on this page. Moving to next...";
                await MoveToNextCivitAiPageAsync();
                return;
            }

            foreach (var model in response.Items.DistinctBy(x => x.Id))
            {
                if (model.ModelVersions is not { Count: > 0 })
                {
                    continue;
                }

                foreach (var version in model.ModelVersions.DistinctBy(x => x.Id))
                {
                    if (version.Files is not { Count: > 0 })
                    {
                        continue;
                    }

                    CivitAiCurrentPageModels.Add(
                        new CivitModelSelectionItem
                        {
                            Model = model,
                            ModelVersion = version,
                            IsSelected = false,
                        }
                    );
                }
            }

            if (CivitAiCurrentPageModels.Count == 0)
            {
                CivitAiSelectionStatus = "No downloadable model versions found on this page. Moving to next...";
                await MoveToNextCivitAiPageAsync();
                return;
            }

            activeCivitAiContext.NextCursor = response.Metadata?.NextCursor;
            activeCivitAiContext.NextPage = ParseQueryInt(response.Metadata?.NextPage);

            HasMoreCivitAiPages =
                !string.IsNullOrWhiteSpace(activeCivitAiContext.NextCursor)
                || activeCivitAiContext.NextPage is > 0;

            CivitAiSelectionStatus =
                $"{CivitAiCurrentPageModels.Count} model(s) available from {CivitAiCurrentPageUrl}";
            var pageLabelSource = "CivitAI page";
            if (!string.IsNullOrWhiteSpace(activeCivitAiContext.Request.Query))
            {
                pageLabelSource = $"Search: {activeCivitAiContext.Request.Query}";
            }
            else if (!string.IsNullOrWhiteSpace(activeCivitAiContext.Request.Tag))
            {
                pageLabelSource = $"Tag: #{activeCivitAiContext.Request.Tag}";
            }
            else if (!string.IsNullOrWhiteSpace(activeCivitAiContext.Request.Username))
            {
                pageLabelSource = $"User: @{activeCivitAiContext.Request.Username}";
            }
            else if (!string.IsNullOrWhiteSpace(activeCivitAiContext.Request.CommaSeparatedModelIds))
            {
                pageLabelSource = $"Models: {activeCivitAiContext.Request.CommaSeparatedModelIds}";
            }

            if (activeCivitAiContext.Request.Page is > 0)
            {
                pageLabelSource += $" (Page {activeCivitAiContext.Request.Page})";
            }

            CivitAiCurrentPageLabel = pageLabelSource;
        }
        finally
        {
            IsImporting = false;
        }
    }

    private async Task<bool> TryImportCivitModel(CivitModel model, CivitModelVersion modelVersion)
    {
        var modelFile =
            modelVersion.Files?.FirstOrDefault(file => file.Type == CivitFileType.Model)
            ?? modelVersion.Files?.FirstOrDefault();

        if (modelFile is null)
        {
            notificationService.Show(
                new Notification(
                    "Model has no files",
                    $"{model.Name} ({modelVersion.Name}) has no downloadable files",
                    NotificationType.Warning
                )
            );
            return false;
        }

        await modelImportService.DoImport(
            model,
            activeDownloadFolder,
            selectedVersion: modelVersion,
            selectedFile: modelFile
        );

        return true;
    }

    private void ResetCivitAiSelectionFlow()
    {
        IsCivitAiSelectionMode = false;
        CivitAiCurrentPageModels.Clear();
        CivitAiCurrentPageLabel = string.Empty;
        CivitAiCurrentPageUrl = string.Empty;
        CivitAiSelectionStatus = string.Empty;
        HasMoreCivitAiPages = false;
        activeCivitAiContext = null;
        civitAiImportQueue.Clear();
        ImportStatus = string.Empty;
    }

    private sealed class CivitAiImportContext
    {
        public required CivitModelsRequest Request { get; init; }
        public required Uri SourceUrl { get; init; }
        public required string SourceLabel { get; init; }
        public string? CurrentCursor { get; set; }
        public string? NextCursor { get; set; }
        public int? NextPage { get; set; }
    }

    public sealed partial class CivitModelSelectionItem : ObservableObject
    {
        [ObservableProperty]
        private bool isSelected;

        public required CivitModel Model { get; init; }
        public required CivitModelVersion ModelVersion { get; init; }

        public string DisplayName => $"{Model.Name} - {ModelVersion.Name}";

        public string Details =>
            $"{Model.Type} • {ModelVersion.BaseModel ?? "Unknown Base"} • {Model.Creator?.Username}";
    }

    [RelayCommand]
    private async Task SelectCustomFolder()
    {
        if (!App.StorageProvider.CanPickFolder)
        {
            notificationService.Show(
                new Notification(
                    "Custom folder not available",
                    "The platform does not support folder picking",
                    NotificationType.Warning
                )
            );
            return;
        }

        var files = await App.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Select Download Folder",
                AllowMultiple = false,
            }
        );

        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
        {
            CustomDownloadLocation = path;
            SelectedDownloadLocation = CustomLocationLabel;
            ImportStatus = $"Custom folder set: {path}";
            return;
        }

        SelectedDownloadLocation = GetLastValidDownloadLocation();
    }

    private string GetLastValidDownloadLocation()
    {
        if (!string.IsNullOrWhiteSpace(lastValidDownloadLocation))
        {
            return lastValidDownloadLocation;
        }

        return AvailableDownloadLocations.FirstOrDefault(loc => !IsCustomLocation(loc)) ?? string.Empty;
    }
}
