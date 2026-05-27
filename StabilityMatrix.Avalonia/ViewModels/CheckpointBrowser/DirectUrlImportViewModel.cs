using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using StabilityMatrix.Avalonia;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;

[View(typeof(Views.DirectUrlImportPage))]
[RegisterSingleton<DirectUrlImportViewModel>]
public partial class DirectUrlImportViewModel : TabViewModelBase
{
    private static string CustomLocationLabel => Resources.Label_CustomEllipsis;
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

    public override string Header => Resources.Label_DirectUrl;

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
        if (AvailableDownloadLocations.Count > 0 && string.IsNullOrWhiteSpace(SelectedDownloadLocation))
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
        var locations = Enum
            .GetValues<SharedFolderType>()
            .Where(folderType => folderType != SharedFolderType.Unknown)
            .Select(folderType => $"Models/{folderType.GetStringValue()}")
            .ToList();
        locations.Add(CustomLocationLabel);
        AvailableDownloadLocations = locations;
    }

    [RelayCommand]
    private async Task Import()
    {
        if (string.IsNullOrWhiteSpace(UrlsInput))
        {
            ShowNotification(
                Resources.Label_NoUrlsProvided,
                Resources.Text_PleaseEnterAtLeastOneUrl,
                NotificationType.Warning
            );
            return;
        }

        var downloadFolder = GetValidatedDownloadFolder();
        if (downloadFolder is null)
        {
            return;
        }

        try
        {
            IsImporting = true;
            ImportStatus = Resources.Text_ParsingUrls;

            var parsedUrls = ParseUrls(UrlsInput);
            if (parsedUrls.Count == 0)
            {
                ShowNotification(
                    Resources.Label_InvalidUrl,
                    Resources.Text_PleaseEnterAtLeastOneValidUrl,
                    NotificationType.Warning
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
                ShowNotification(
                    Resources.Label_NoFileNameProvided,
                    Resources.Text_PleaseEnterAFileNameForFallbackDirectDownloads,
                    NotificationType.Warning
                );
                return;
            }

            if (directUrls.Count > 0)
            {
                ImportStatus = string.Format(
                    Resources.Text_DownloadingFromDirectUrls,
                    directUrls.Count
                );
                await modelImportService.DoCustomImport(directUrls, ModelFileName, downloadFolder);

                var downloadMessage = string.Format(
                    Resources.Label_DownloadWillBeSavedToLocation,
                    ModelFileName,
                    downloadFolder
                );
                ImportStatus = downloadMessage;
                ShowNotification(
                    Resources.Label_DownloadStarted,
                    downloadMessage,
                    NotificationType.Success
                );
            }

            if (civitAiContexts.Count > 0)
            {
                StartCivitAiSelectionFlow(civitAiContexts);
                ImportStatus = Resources.Text_LoadingCivitAiModels;
                await LoadCurrentCivitAiPageAsync();
                return;
            }

            UrlsInput = string.Empty;
            ModelFileName = string.Empty;
            ImportStatus = string.Empty;
        }
        catch (UriFormatException ex)
        {
            ShowNotification(Resources.Label_InvalidUrl, ex.Message, NotificationType.Error);
        }
        catch (Exception ex)
        {
            ShowNotification(Resources.Label_ImportFailed, ex.Message, NotificationType.Error);
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

        foreach (
            var line in rawInput.Split(
                UrlLineSeparators,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
        )
        {
            if (!Uri.TryCreate(line, UriKind.Absolute, out var parsed))
            {
                throw new UriFormatException(string.Format(Resources.Text_InvalidUrlFormat, line));
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
        var query = HttpUtility.ParseQueryString(uri.Query);

        var pathParts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pathParts.Length > 1 && int.TryParse(pathParts[1], out var modelId))
        {
            request.CommaSeparatedModelIds = modelId.ToString();
            context = new CivitAiImportContext
            {
                Request = request,
                SourceLabel = $"https://{CivitAiHost}/models/{modelId}",
            };

            return true;
        }

        var queryValue = query["query"];
        var tagValue = query["tag"];
        var usernameValue = query["username"];
        var idsValue = query["ids"];
        var modelIdValue = query["modelId"];
        var sortValue = query["sort"];
        var periodValue = query["period"];
        var cursorValue = query["cursor"];
        var pageValue = query["page"];
        var limitValue = query["limit"];
        var typesValue = query["types"];
        var baseModelsValue = query["baseModels"];

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
            ShowNotification(
                Resources.Label_NoModelsSelected,
                Resources.Text_PleaseEnterAtLeastOneModelFromThisPage,
                NotificationType.Warning
            );
            return;
        }

        var downloadFolder = GetValidatedDownloadFolder();
        if (downloadFolder is null)
        {
            return;
        }

        IsImporting = true;

        try
        {
            var started = 0;
            var failed = 0;

            foreach (var item in selected)
            {
                if (await TryImportCivitModel(item.Model, item.ModelVersion, downloadFolder))
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
                ShowNotification(
                    Resources.Label_SomeImportsFailed,
                    string.Format(Resources.Text_StartedModelsSkippedModels, started, failed),
                    NotificationType.Warning
                );
            }
            else
            {
                ShowNotification(
                    Resources.Label_ImportsStarted,
                    string.Format(Resources.Text_StartedModels, started),
                    NotificationType.Success
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
        if (!TryAdvanceToNextCivitAiPage())
        {
            CompleteCivitAiSelectionFlow();
            return;
        }

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
            while (activeCivitAiContext is not null)
            {
                var request = activeCivitAiContext.Request.Clone();
                if (!string.IsNullOrWhiteSpace(activeCivitAiContext.CurrentCursor))
                {
                    request.Cursor = activeCivitAiContext.CurrentCursor;
                }

                var response = await civitApi.GetModels(request);
                var pageModels = CreateSelectionItems(response.Items);

                if (pageModels.Count == 0)
                {
                    CivitAiSelectionStatus = response.Items is { Count: > 0 }
                        ? Resources.Text_NoDownloadableModelVersionsFoundOnThisPageMovingToNext
                        : Resources.Text_NoModelsFoundOnThisPageMovingToNext;

                    if (!TryAdvanceToNextCivitAiPage())
                    {
                        CompleteCivitAiSelectionFlow();
                        return;
                    }

                    continue;
                }

                SetCurrentCivitAiPage(response, pageModels);
                return;
            }

            CompleteCivitAiSelectionFlow();
        }
        finally
        {
            IsImporting = false;
        }
    }

    private async Task<bool> TryImportCivitModel(
        CivitModel model,
        CivitModelVersion modelVersion,
        DirectoryPath downloadFolder
    )
    {
        var modelFile =
            modelVersion.Files?.FirstOrDefault(file => file.Type == CivitFileType.Model)
            ?? modelVersion.Files?.FirstOrDefault();

        if (modelFile is null)
        {
            ShowNotification(
                Resources.Label_ModelHasNoFiles,
                string.Format(
                    Resources.Text_ModelHasNoDownloadableFiles,
                    model.Name,
                    modelVersion.Name
                ),
                NotificationType.Warning
            );
            return false;
        }

        await modelImportService.DoImport(
            model,
            downloadFolder,
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
            $"{Model.Type} • {ModelVersion.BaseModel ?? Resources.Label_UnknownBase} • {Model.Creator?.Username}";
    }

    [RelayCommand]
    private async Task SelectCustomFolder()
    {
        if (!App.StorageProvider.CanPickFolder)
        {
            ShowNotification(
                Resources.Label_SelectDownloadFolder,
                Resources.Text_PlatformDoesNotSupportFolderPicking,
                NotificationType.Warning
            );
            return;
        }

        var files = await App.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = Resources.Label_SelectDownloadFolder,
                AllowMultiple = false,
            }
        );

        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
        {
            CustomDownloadLocation = path;
            SelectedDownloadLocation = CustomLocationLabel;
            ImportStatus = string.Format(Resources.Text_CustomFolderSet, path);
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

    private DirectoryPath? GetValidatedDownloadFolder()
    {
        if (string.IsNullOrWhiteSpace(SelectedDownloadLocation))
        {
            ShowNotification(
                Resources.Label_SelectDownloadLocation,
                Resources.Text_PleaseSelectADownloadLocation,
                NotificationType.Warning
            );
            return null;
        }

        if (IsCustomLocation(SelectedDownloadLocation) && string.IsNullOrWhiteSpace(CustomDownloadLocation))
        {
            ShowNotification(
                Resources.Label_SelectDownloadLocation,
                Resources.Text_PleasePickAFolderForTheCustomLocation,
                NotificationType.Warning
            );
            return null;
        }

        return ResolveDownloadFolder();
    }

    private bool TryAdvanceToNextCivitAiPage()
    {
        if (activeCivitAiContext is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(activeCivitAiContext.NextCursor))
        {
            activeCivitAiContext.CurrentCursor = activeCivitAiContext.NextCursor;
            activeCivitAiContext.NextCursor = null;
            return true;
        }

        if (activeCivitAiContext.NextPage is > 0)
        {
            activeCivitAiContext.CurrentCursor = null;
            activeCivitAiContext.Request.Page = activeCivitAiContext.NextPage;
            activeCivitAiContext.Request.Cursor = null;
            activeCivitAiContext.NextPage = null;
            return true;
        }

        if (!civitAiImportQueue.TryDequeue(out var nextContext))
        {
            return false;
        }

        activeCivitAiContext = nextContext;
        CivitAiCurrentPageUrl = nextContext.SourceLabel;
        return true;
    }

    private static List<CivitModelSelectionItem> CreateSelectionItems(IReadOnlyList<CivitModel>? models)
    {
        var items = new List<CivitModelSelectionItem>();
        if (models is not { Count: > 0 })
        {
            return items;
        }

        foreach (var model in models.DistinctBy(x => x.Id))
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

                items.Add(
                    new CivitModelSelectionItem
                    {
                        Model = model,
                        ModelVersion = version,
                    }
                );
            }
        }

        return items;
    }

    private void SetCurrentCivitAiPage(
        CivitModelsResponse response,
        IReadOnlyCollection<CivitModelSelectionItem> pageModels
    )
    {
        CivitAiCurrentPageModels.Clear();
        foreach (var item in pageModels)
        {
            CivitAiCurrentPageModels.Add(item);
        }

        activeCivitAiContext!.NextCursor = response.Metadata?.NextCursor;
        activeCivitAiContext.NextPage = ParseQueryInt(response.Metadata?.NextPage);
        HasMoreCivitAiPages =
            !string.IsNullOrWhiteSpace(activeCivitAiContext.NextCursor)
            || activeCivitAiContext.NextPage is > 0;
        CivitAiCurrentPageLabel = FormatCivitAiPageLabel(activeCivitAiContext);
        CivitAiSelectionStatus = string.Format(
            Resources.Text_CivitAiModelsAvailableFrom,
            CivitAiCurrentPageModels.Count,
            CivitAiCurrentPageUrl
        );
    }

    private static string FormatCivitAiPageLabel(CivitAiImportContext context)
    {
        var pageLabelSource = Resources.Text_CivitAiPage;
        if (!string.IsNullOrWhiteSpace(context.Request.Query))
        {
            pageLabelSource = string.Format(Resources.Text_SearchFormat, context.Request.Query);
        }
        else if (!string.IsNullOrWhiteSpace(context.Request.Tag))
        {
            pageLabelSource = string.Format(Resources.Text_TagFormat, context.Request.Tag);
        }
        else if (!string.IsNullOrWhiteSpace(context.Request.Username))
        {
            pageLabelSource = string.Format(Resources.Text_UserFormat, context.Request.Username);
        }
        else if (!string.IsNullOrWhiteSpace(context.Request.CommaSeparatedModelIds))
        {
            pageLabelSource = string.Format(
                Resources.Text_ModelsFormat,
                context.Request.CommaSeparatedModelIds
            );
        }

        if (context.Request.Page is > 0)
        {
            pageLabelSource = string.Format(Resources.Text_PageFormat, pageLabelSource, context.Request.Page);
        }

        return pageLabelSource;
    }

    private void CompleteCivitAiSelectionFlow()
    {
        ResetCivitAiSelectionFlow();
        UrlsInput = string.Empty;
        ModelFileName = string.Empty;
    }

    private void ShowNotification(string title, string message, NotificationType notificationType)
    {
        notificationService.Show(new Notification(title, message, notificationType));
    }
}
