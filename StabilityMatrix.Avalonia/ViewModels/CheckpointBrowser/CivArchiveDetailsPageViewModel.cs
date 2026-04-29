using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using AsyncAwaitBestPractices;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using FluentAvalonia.Core;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Api.CivArchive;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;

[View(typeof(CivArchiveDetailsPage))]
[ManagedService]
[RegisterTransient<CivArchiveDetailsPageViewModel>]
public partial class CivArchiveDetailsPageViewModel(
    ICivArchiveApiClient civArchiveApiClient,
    INavigationService<MainWindowViewModel> navigationService,
    IServiceManager<ViewModelBase> vmFactory,
    IModelImportService modelImportService,
    ISettingsManager settingsManager,
    INotificationService notificationService,
    IModelIndexService modelIndexService
) : DisposableViewModelBase
{
    private static readonly string[] IgnoredFileNameFormatVars =
    [
        "seed",
        "prompt",
        "negative_prompt",
        "model_hash",
        "sampler",
        "cfgscale",
        "steps",
        "width",
        "height",
        "project_type",
        "project_name",
    ];

    public IEnumerable<FileNameFormatVar> ModelFileNameFormatVars =>
        FileNameFormatProvider
            .GetSampleForModelBrowser()
            .Substitutions.Where(kv => !IgnoredFileNameFormatVars.Contains(kv.Key))
            .Select(kv => new FileNameFormatVar { Variable = $"{{{kv.Key}}}", Example = kv.Value.Invoke() });

    [ObservableProperty]
    public partial string RelativeUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial CivArchiveModelDetails? Model { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string ErrorText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial CivArchiveVersionReference? SelectedVersion { get; set; }

    [ObservableProperty]
    public partial string ModelDescriptionHtml { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string VersionDescriptionHtml { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasDownloadUrl { get; set; }

    [ObservableProperty]
    public partial bool IsInstalled { get; set; }

    [ObservableProperty]
    public partial string? InstalledLocation { get; set; }

    [ObservableProperty]
    [CustomValidation(typeof(CivArchiveDetailsPageViewModel), nameof(ValidateModelFileNameFormat))]
    public partial string? ModelFileNameFormat { get; set; }

    [ObservableProperty]
    public partial string? ModelNameFormatSample { get; set; }

    public ObservableCollection<CivArchiveModelImage> Images { get; } = [];
    public ObservableCollection<CivArchiveModelFile> Files { get; } = [];
    public ObservableCollection<CivArchiveVersionMirror> Mirrors { get; } = [];

    private static readonly Dictionary<
        string,
        (SharedFolderType Folder, CivitModelType ModelType)
    > ModelTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Checkpoint"] = (SharedFolderType.StableDiffusion, CivitModelType.Checkpoint),
        ["LORA"] = (SharedFolderType.Lora, CivitModelType.LORA),
        ["DoRA"] = (SharedFolderType.Lora, CivitModelType.DoRA),
        ["LoCon"] = (SharedFolderType.LyCORIS, CivitModelType.LoCon),
        ["TextualInversion"] = (SharedFolderType.Embeddings, CivitModelType.TextualInversion),
        ["Hypernetwork"] = (SharedFolderType.Hypernetwork, CivitModelType.Hypernetwork),
        ["Controlnet"] = (SharedFolderType.ControlNet, CivitModelType.Controlnet),
        ["VAE"] = (SharedFolderType.VAE, CivitModelType.VAE),
        ["Upscaler"] = (SharedFolderType.ESRGAN, CivitModelType.Upscaler),
    };

    protected override async Task OnInitialLoadedAsync()
    {
        await base.OnInitialLoadedAsync();

        AddDisposable(
            settingsManager.RelayPropertyFor(
                this,
                vm => vm.ModelFileNameFormat,
                settings => settings.CivitModelBrowserFileNamePattern,
                true
            )
        );

        AddDisposable(
            this.WhenPropertyChanged(vm => vm.ModelFileNameFormat)
                .Throttle(TimeSpan.FromMilliseconds(50))
                .ObserveOn(SynchronizationContext.Current!)
                .Subscribe(_ => UpdateNameFormatSample())
        );

        AddDisposable(
            this.WhenPropertyChanged(vm => vm.Model)
                .ObserveOn(SynchronizationContext.Current!)
                .Subscribe(_ => UpdateNameFormatSample())
        );

        // Refresh the IsInstalled badge / Download button label when the user downloads
        // (or deletes) this model — without forcing them to navigate away and back.
        EventHandler indexChangedHandler = (_, _) =>
            Dispatcher.UIThread.Post(() => UpdateInstalledStatus(Model?.Version));
        EventManager.Instance.ModelIndexChanged += indexChangedHandler;
        AddDisposable(
            Disposable.Create(() => EventManager.Instance.ModelIndexChanged -= indexChangedHandler)
        );
    }

    public override async Task OnLoadedAsync()
    {
        await base.OnLoadedAsync();

        if (IsLoading || string.IsNullOrWhiteSpace(RelativeUrl))
        {
            return;
        }

        await LoadModelAsync();
    }

    public static ValidationResult ValidateModelFileNameFormat(string? format, ValidationContext context)
    {
        return FileNameFormatProvider.GetSampleForModelBrowser().Validate(format ?? string.Empty);
    }

    private void UpdateNameFormatSample()
    {
        var provider = BuildFormatProvider(Model?.Version, GetPrimaryFile(Model?.Version));
        var format = ParseFormatOrDefault(ModelFileNameFormat, provider);

        var sample = NormalizePathSegments(format.GetFileName());
        ModelNameFormatSample = string.IsNullOrWhiteSpace(sample)
            ? null
            : "Example: " + sample + ".safetensors";
    }

    /// <summary>
    /// Strip empty path segments left behind by null/empty substitutions, so a pattern
    /// like <c>{base_model}/{file_name}</c> with an empty base_model collapses to
    /// <c>file_name</c> instead of <c>/file_name</c>.
    /// Also drops <c>..</c> / <c>.</c> traversal segments so a pattern variable that
    /// resolves to <c>..</c> can't escape the destination folder.
    /// </summary>
    private static string NormalizePathSegments(string raw)
    {
        if (string.IsNullOrEmpty(raw) || (!raw.Contains('/') && !raw.Contains('\\')))
            return raw;

        var parts = raw.Split(
                ['/', '\\'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
            .Where(p => p != ".." && p != ".");
        return string.Join('/', parts);
    }

    /// <summary>
    /// Parse a template against the provider, falling back to the default template if the input is empty,
    /// references unknown variables, or fails to parse for any reason. Validate() must be called first
    /// because Parse() throws KeyNotFoundException on unknown variables (e.g. mid-typing "{base}" before
    /// the user finishes "{base_model}").
    /// </summary>
    private static FileNameFormat ParseFormatOrDefault(string? template, FileNameFormatProvider provider)
    {
        if (
            !string.IsNullOrEmpty(template)
            && provider.Validate(template) == ValidationResult.Success
            && FileNameFormat.TryParse(template, provider, out var format)
        )
        {
            return format;
        }

        return FileNameFormat.Parse(FileNameFormat.DefaultModelBrowserTemplate, provider);
    }

    private FileNameFormatProvider BuildFormatProvider(
        CivArchiveModelVersion? version,
        CivArchiveModelFile? primaryFile
    )
    {
        if (Model is null)
        {
            return new FileNameFormatProvider();
        }

        // Build CivitModel-shaped stubs so FileNameFormatProvider can resolve {model_name}, {file_name}, etc.
        var modelType = CivitModelType.Unknown;
        if (Model.Type is not null && ModelTypeMap.TryGetValue(Model.Type, out var mapping))
        {
            modelType = mapping.ModelType;
        }

        var synthesizedFileName = string.IsNullOrWhiteSpace(version?.Name)
            ? $"{Model.Name}.safetensors"
            : $"{Model.Name}_{version.Name}.safetensors";

        var civitModel = new CivitModel
        {
            Id = int.TryParse(Model.Id, out var modelId) ? modelId : 0,
            Name = Model.Name,
            Type = modelType,
            Creator = new CivitCreator { Username = Model.CreatorUsername ?? Model.Username },
        };

        var civitVersion = version is null
            ? null
            : new CivitModelVersion
            {
                Id = int.TryParse(version.Id, out var versionId) ? versionId : 0,
                Name = version.Name,
                BaseModel = version.BaseModel,
            };

        var civitFile = new CivitFile
        {
            Id = int.TryParse(primaryFile?.Id, out var fileId) ? fileId : 0,
            Name = !string.IsNullOrWhiteSpace(primaryFile?.Name) ? primaryFile.Name : synthesizedFileName,
        };

        return new FileNameFormatProvider
        {
            CivitModel = civitModel,
            CivitModelVersion = civitVersion,
            CivitFile = civitFile,
        };
    }

    private async Task LoadModelAsync()
    {
        IsLoading = true;
        ErrorText = string.Empty;

        try
        {
            var response = await civArchiveApiClient.GetModelDetailsAsync(RelativeUrl);
            Model = response.Model;

            ModelDescriptionHtml = WrapHtml(response.Model.Description);
            PopulateVersionData(response.Model.Version);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void PopulateVersionData(CivArchiveModelVersion? version)
    {
        VersionDescriptionHtml = WrapHtml(version?.Description);
        HasDownloadUrl = GetDownloadUris(version).Count > 0;

        Images.Clear();
        foreach (var image in version?.Images.Where(IsUsableImage) ?? [])
        {
            Images.Add(image);
        }

        Files.Clear();
        foreach (var file in version?.Files ?? [])
        {
            Files.Add(file);
        }

        Mirrors.Clear();
        foreach (var mirror in version?.Mirrors ?? [])
        {
            Mirrors.Add(mirror);
        }

        UpdateInstalledStatus(version);
    }

    private void UpdateInstalledStatus(CivArchiveModelVersion? version)
    {
        // First try URL match — works for every platform, including ones where file hashes are missing.
        var installedUrls = modelIndexService.ModelIndexCivArchiveUrls;
        if (!string.IsNullOrWhiteSpace(RelativeUrl) && installedUrls.Contains(RelativeUrl))
        {
            IsInstalled = true;
            InstalledLocation = LookupInstalledLocationByUrl(RelativeUrl);
            return;
        }

        // Fallback to SHA256 match — catches CivitAI mirrors with full file hashes,
        // including models downloaded via SM before SourceUrl tracking existed.
        var hashes = version
            ?.Files.Select(f => f.Sha256)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Cast<string>()
            .ToList();

        if (hashes is null || hashes.Count == 0)
        {
            IsInstalled = false;
            InstalledLocation = null;
            return;
        }

        var installedHashes = modelIndexService.ModelIndexSha256Hashes;
        var matchedHash = hashes.FirstOrDefault(h => installedHashes.Contains(h));

        if (matchedHash is null)
        {
            IsInstalled = false;
            InstalledLocation = null;
            return;
        }

        IsInstalled = true;
        _ = LoadInstalledLocationAsync(matchedHash);
    }

    private string? LookupInstalledLocationByUrl(string sourceUrl)
    {
        return modelIndexService
            .ModelIndex.Values.SelectMany(x => x)
            .FirstOrDefault(m =>
                m.HasCivArchiveMetadata
                && string.Equals(
                    m.ConnectedModelInfo.SourceUrl,
                    sourceUrl,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            ?.RelativePath;
    }

    private async Task LoadInstalledLocationAsync(string sha256)
    {
        try
        {
            var matches = await modelIndexService.FindBySha256Async(sha256);
            var first = matches?.FirstOrDefault();
            InstalledLocation = first?.RelativePath;
        }
        catch
        {
            InstalledLocation = null;
        }
    }

    private static string WrapHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        return $"""<html><body class="markdown-body">{html}</body></html>""";
    }

    [RelayCommand]
    private async Task ShowImageDialog(CivArchiveModelImage? image)
    {
        if (image?.Url is null)
        {
            return;
        }

        var currentIndex = Images.IndexOf(image);
        var imageSource = await PrepareImageSourceAsync(image.Url);
        if (imageSource is null)
            return;

        var vm = vmFactory.Get<ImageViewerViewModel>();
        vm.ImageSource = imageSource;

        using var onNav = Observable
            .FromEventPattern<DirectionalNavigationEventArgs>(
                vm,
                nameof(ImageViewerViewModel.NavigationRequested)
            )
            .ObserveOn(SynchronizationContext.Current!)
            .Subscribe(ctx =>
            {
                Dispatcher
                    .UIThread.InvokeAsync(async () =>
                    {
                        var sender = (ImageViewerViewModel)ctx.Sender!;
                        var newIndex = currentIndex + (ctx.EventArgs.IsNext ? 1 : -1);

                        if (newIndex >= 0 && newIndex < Images.Count)
                        {
                            var newImage = Images[newIndex];
                            if (newImage.Url is null)
                            {
                                return;
                            }

                            var newSource = await PrepareImageSourceAsync(newImage.Url);
                            if (newSource is null)
                                return;

                            sender.ImageSource = newSource;
                            currentIndex = newIndex;
                        }
                    })
                    .SafeFireAndForget();
            });

        await vm.GetDialog().ShowAsync();
    }

    /// <summary>
    /// Build an <see cref="ImageSource"/> ready for the image viewer to render.
    /// The viewer's template selector keys off <c>ImageSource.TemplateKey</c>; if that's
    /// <c>Default</c>, the selector renders the literal "Unsupported Format" text. The
    /// URL-construction path leaves TemplateKey as Default until a Task-based binding
    /// resolves it, which races with the viewer's first paint on extensionless CivArchive
    /// CDN URLs (e.g. <c>img.genur.art/sig/.../base64</c>). Use the bitmap-only constructor
    /// instead — it sets TemplateKey to Image synchronously, which the AdvancedImageBox
    /// can render whether the bytes were JPEG, PNG, or WebP.
    /// </summary>
    private static async Task<ImageSource?> PrepareImageSourceAsync(string url)
    {
        try
        {
            var loader = new ImageSource(new Uri(url));
            var bitmap = await loader.GetBitmapAsync();
            return bitmap is not null ? new ImageSource(bitmap) { RemoteUrl = new Uri(url) } : null;
        }
        catch
        {
            return null;
        }
    }

    [RelayCommand]
    private async Task SelectVersion(CivArchiveVersionReference? versionRef)
    {
        if (versionRef is null || string.IsNullOrWhiteSpace(versionRef.Href) || IsLoading)
        {
            return;
        }

        SelectedVersion = versionRef;
        RelativeUrl = versionRef.Href;
        await LoadModelAsync();
    }

    [RelayCommand]
    private void GoBack()
    {
        if (!navigationService.GoBack())
        {
            navigationService.NavigateTo<CheckpointBrowserViewModel>();
        }
    }

    [RelayCommand]
    private void OpenOnCivArchive()
    {
        ProcessRunner.OpenUrl(civArchiveApiClient.GetAbsoluteUri(RelativeUrl).ToString());
    }

    [RelayCommand]
    private async Task DownloadModel()
    {
        var version = Model?.Version;
        if (version is null)
            return;

        var primaryFile = GetPrimaryFile(version);
        await ExecuteDownloadAsync(version, primaryFile, GetDownloadUris(version), sourceLabel: null);
    }

    [RelayCommand]
    private async Task DeleteModel()
    {
        var localFiles = FindLocallyInstalledFiles();
        if (localFiles.Count == 0)
            return;

        var pathsToDelete = new List<string>();
        foreach (var localFile in localFiles)
        {
            var checkpointPath = new FilePath(localFile.GetFullPath(settingsManager.ModelsDirectory));
            if (File.Exists(checkpointPath))
                pathsToDelete.Add(checkpointPath);

            var previewPath = localFile.GetPreviewImageFullPath(settingsManager.ModelsDirectory);
            if (!string.IsNullOrEmpty(previewPath) && File.Exists(previewPath))
                pathsToDelete.Add(previewPath);

            var cmInfoPath = checkpointPath
                .ToString()
                .Replace(checkpointPath.Extension, ConnectedModelInfo.FileExtension);
            if (File.Exists(cmInfoPath))
                pathsToDelete.Add(cmInfoPath);
        }

        if (pathsToDelete.Count == 0)
            return;

        var confirmDeleteVm = vmFactory.Get<ConfirmDeleteDialogViewModel>();
        confirmDeleteVm.PathsToDelete = pathsToDelete;

        var dialog = confirmDeleteVm.GetDialog();
        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
            return;

        try
        {
            await confirmDeleteVm.ExecuteCurrentDeleteOperationAsync(failFast: true);
        }
        catch (Exception ex)
        {
            notificationService.Show("Delete failed", ex.Message);
        }
        finally
        {
            await modelIndexService.RefreshIndex();
            // RefreshIndex fires ModelIndexChanged → our handler updates IsInstalled
            // and the UI flips back to "Download" automatically.
        }
    }

    /// <summary>
    /// Find every locally installed file matching this model — try the SourceUrl first,
    /// fall back to SHA256 hash matches for legacy downloads without SourceUrl.
    /// </summary>
    private List<LocalModelFile> FindLocallyInstalledFiles()
    {
        var matches = new List<LocalModelFile>();
        var allLocal = modelIndexService.ModelIndex.Values.SelectMany(x => x).ToList();

        if (!string.IsNullOrWhiteSpace(RelativeUrl))
        {
            matches.AddRange(
                allLocal.Where(m =>
                    m.HasCivArchiveMetadata
                    && string.Equals(
                        m.ConnectedModelInfo.SourceUrl,
                        RelativeUrl,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            );
        }

        if (matches.Count == 0 && Model?.Version is { } version)
        {
            var hashes = version
                .Files.Select(f => f.Sha256)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (hashes.Count > 0)
            {
                matches.AddRange(
                    allLocal.Where(m =>
                        !string.IsNullOrWhiteSpace(m.HashSha256) && hashes.Contains(m.HashSha256)
                    )
                );
            }
        }

        return matches;
    }

    [RelayCommand]
    private async Task DownloadFile(CivArchiveModelFile? file)
    {
        var version = Model?.Version;
        if (version is null || file is null)
            return;

        await ExecuteDownloadAsync(version, file, GetDownloadUrisForFile(file), sourceLabel: null);
    }

    [RelayCommand]
    private async Task DownloadFromMirror(CivArchiveFileMirror? mirror)
    {
        if (mirror is null || string.IsNullOrWhiteSpace(mirror.Url))
            return;

        // Gated/paid mirrors require auth or payment we can't handle in-app — open externally.
        if (mirror.IsGated || mirror.IsPaid)
        {
            ProcessRunner.OpenUrl(mirror.Url);
            return;
        }

        var version = Model?.Version;
        if (version is null)
            return;

        // Find the parent file so we can attach hash + cm-info correctly.
        var parentFile = version.Files.FirstOrDefault(f => f.Mirrors.Contains(mirror));

        await ExecuteDownloadAsync(
            version,
            parentFile,
            [civArchiveApiClient.GetAbsoluteUri(mirror.Url)],
            sourceLabel: mirror.Source
        );
    }

    private async Task ExecuteDownloadAsync(
        CivArchiveModelVersion version,
        CivArchiveModelFile? file,
        IReadOnlyList<Uri> downloadUris,
        string? sourceLabel
    )
    {
        if (Model is null)
            return;

        if (downloadUris.Count == 0)
        {
            notificationService.Show(
                "No download available",
                "This file has no usable download URL — every mirror was either missing or gated/paid."
            );
            return;
        }

        if (!settingsManager.IsLibraryDirSet)
        {
            notificationService.Show("Download Failed", "Please set a library directory in settings first.");
            return;
        }

        var destinationDir = GetDefaultDownloadFolder();
        var fileName = BuildDownloadFileName(version, file);

        Uri? previewImageUri = null;
        string? previewImageExtension = null;
        var firstImage = version.Images.FirstOrDefault(IsUsableImage);
        if (firstImage?.Url is not null)
        {
            previewImageUri = new Uri(firstImage.Url);
            previewImageExtension = ResolvePreviewImageExtension(previewImageUri);
        }

        var connectedModelInfo = BuildConnectedModelInfo(Model, version, RelativeUrl);
        // Override hash so the cm-info matches the specific file being downloaded
        // (BuildConnectedModelInfo defaults to the primary file's hash).
        if (!string.IsNullOrWhiteSpace(file?.Sha256))
        {
            connectedModelInfo.Hashes = new CivitFileHashes { SHA256 = file.Sha256 };
        }

        await modelImportService.DoCustomImport(
            downloadUris,
            fileName,
            destinationDir,
            previewImageUri,
            previewImageFileExtension: previewImageExtension,
            connectedModelInfo: connectedModelInfo,
            configureDownload: download =>
            {
                if (!string.IsNullOrWhiteSpace(file?.Sha256))
                {
                    download.ExpectedHashSha256 = file.Sha256;
                }

                // The CivitAI flow uses CivitPostDownloadContextAction to refresh the
                // model index post-download; we don't have an analogous context action
                // (we rely on cm-info instead of Blake3 hash), so subscribe directly to
                // ProgressStateChanged. Refreshing the index fires ModelIndexChanged,
                // which our OnInitialLoadedAsync subscription uses to flip the Installed
                // badge / "Download again" label live.
                download.ProgressStateChanged += (_, state) =>
                {
                    if (state == ProgressState.Success)
                    {
                        modelIndexService.BackgroundRefreshIndex();
                    }
                };
            }
        );

        var finalPath = destinationDir.JoinFile(fileName);
        var sourceText = string.IsNullOrEmpty(sourceLabel) ? string.Empty : $" from {sourceLabel}";
        notificationService.Show(
            "Download Started",
            $"{finalPath.Name}{sourceText} will be saved to {finalPath.Directory}"
        );
    }

    /// <summary>
    /// CivArchive aggregates images from many platforms; some URLs don't end in a recognizable
    /// extension (e.g. CivitAI's "/width=512/img" style paths or extension-less CDN URLs). Try
    /// Path.GetExtension first, then scan the raw URL for a known image extension, then fall back
    /// to ".jpeg" so the import never fails at the preview-image step.
    /// </summary>
    private static string ResolvePreviewImageExtension(Uri previewImageUri)
    {
        var fromPath = Path.GetExtension(previewImageUri.LocalPath);
        if (!string.IsNullOrWhiteSpace(fromPath))
            return fromPath;

        ReadOnlySpan<string> known = [".jpeg", ".jpg", ".png", ".webp", ".gif", ".avif"];
        var raw = previewImageUri.ToString();
        foreach (var ext in known)
        {
            if (raw.Contains(ext, StringComparison.OrdinalIgnoreCase))
                return ext;
        }

        return ".jpeg";
    }

    private IReadOnlyList<Uri> GetDownloadUrisForFile(CivArchiveModelFile file)
    {
        var urlCandidates = new List<string?> { file.DownloadUrl };
        if (file.Mirrors is not null)
        {
            urlCandidates.AddRange(file.Mirrors.Where(m => !m.IsGated && !m.IsPaid).Select(m => m.Url));
        }

        return urlCandidates
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => civArchiveApiClient.GetAbsoluteUri(url!))
            .Distinct()
            .ToList();
    }

    private IReadOnlyList<Uri> GetDownloadUris(CivArchiveModelVersion? version)
    {
        if (version is null)
        {
            return [];
        }

        var primaryFile = GetPrimaryFile(version);
        var urlCandidates = new List<string?> { version.DownloadUrl, primaryFile?.DownloadUrl };

        if (primaryFile?.Mirrors is not null)
        {
            urlCandidates.AddRange(primaryFile.Mirrors.Select(mirror => mirror.Url));
        }

        return urlCandidates
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => civArchiveApiClient.GetAbsoluteUri(url!))
            .Distinct()
            .ToList();
    }

    private static CivArchiveModelFile? GetPrimaryFile(CivArchiveModelVersion? version)
    {
        if (version is null)
        {
            return null;
        }

        return version.Files.FirstOrDefault(f => f.IsPrimary) ?? version.Files.FirstOrDefault();
    }

    private string BuildDownloadFileName(CivArchiveModelVersion version, CivArchiveModelFile? primaryFile)
    {
        var extension = !string.IsNullOrWhiteSpace(primaryFile?.Name)
            ? Path.GetExtension(primaryFile.Name)
            : ".safetensors";
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".safetensors";
        }

        var provider = BuildFormatProvider(version, primaryFile);
        var format = ParseFormatOrDefault(ModelFileNameFormat, provider);

        // Normalize so a leading "/" from an empty {base_model} doesn't make Path.Combine
        // treat the name as rooted and drop the destination folder.
        var stem = NormalizePathSegments(format.GetFileName());

        if (string.IsNullOrWhiteSpace(stem))
        {
            // Pattern resolved to empty (e.g. only {file_name} on a non-CivitAI mirror with no primary file).
            // Fall back to a sensible synthesized name.
            stem = string.IsNullOrWhiteSpace(version.Name)
                ? Model?.Name ?? "model"
                : $"{Model?.Name ?? "model"}_{version.Name}";
        }

        return stem + extension;
    }

    private DirectoryPath GetDefaultDownloadFolder()
    {
        var modelType = Model?.Type;
        if (modelType is not null && ModelTypeMap.TryGetValue(modelType, out var mapping))
        {
            return new DirectoryPath(settingsManager.ModelsDirectory, mapping.Folder.GetStringValue());
        }

        return new DirectoryPath(settingsManager.ModelsDirectory);
    }

    private static ConnectedModelInfo BuildConnectedModelInfo(
        CivArchiveModelDetails model,
        CivArchiveModelVersion version,
        string sourceUrl
    )
    {
        var civitModelType = CivitModelType.Unknown;
        if (model.Type is not null && ModelTypeMap.TryGetValue(model.Type, out var mapping))
        {
            civitModelType = mapping.ModelType;
        }

        var primaryFile = version.Files.FirstOrDefault(f => f.IsPrimary) ?? version.Files.FirstOrDefault();

        return new ConnectedModelInfo
        {
            ModelName = model.Name,
            ModelDescription = model.Description ?? string.Empty,
            Nsfw = model.IsNsfw,
            Tags = model.Tags.ToArray(),
            ModelType = civitModelType,
            VersionName = version.Name,
            VersionDescription = version.Description,
            BaseModel = version.BaseModel,
            ImportedAt = DateTimeOffset.UtcNow,
            Hashes = new CivitFileHashes { SHA256 = primaryFile?.Sha256 },
            TrainedWords = version.Trigger.ToArray(),
            ThumbnailImageUrl = version.Images.FirstOrDefault(IsUsableImage)?.Url,
            Source = ConnectedModelSource.CivArchive,
            SourceUrl = sourceUrl,
            Stats = new CivitModelStats
            {
                DownloadCount = (int)model.DownloadCount,
                FavoriteCount = (int)model.FavoriteCount,
                CommentCount = (int)model.CommentCount,
                RatingCount = (int)model.RatingCount,
                Rating = model.Rating,
            },
        };
    }

    private static bool IsUsableImage(CivArchiveModelImage image)
    {
        return !string.IsNullOrWhiteSpace(image.Url)
            && (
                string.IsNullOrWhiteSpace(image.Type)
                || string.Equals(image.Type, "image", StringComparison.OrdinalIgnoreCase)
            );
    }

    [RelayCommand]
    private void OpenVersionMirror(CivArchiveVersionMirror? mirror)
    {
        if (!string.IsNullOrWhiteSpace(mirror?.PlatformUrl))
        {
            ProcessRunner.OpenUrl(mirror.PlatformUrl);
        }
    }

    [RelayCommand]
    private async Task CopySha256(CivArchiveModelFile? file)
    {
        if (!string.IsNullOrWhiteSpace(file?.Sha256) && App.Clipboard is not null)
        {
            await App.Clipboard.SetTextAsync(file.Sha256);
        }
    }
}
