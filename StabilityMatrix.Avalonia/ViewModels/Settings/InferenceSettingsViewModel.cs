using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Reactive.Linq;
using Avalonia.Controls.Notifications;
using Avalonia.Data;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using FluentIcons.Common;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Settings;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.Avalonia.Fluent.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels.Settings;

[View(typeof(InferenceSettingsPage))]
[ManagedService]
[RegisterSingleton<InferenceSettingsViewModel>]
public partial class InferenceSettingsViewModel : PageViewModelBase
{
    private readonly INotificationService notificationService;
    private readonly ISettingsManager settingsManager;
    private readonly ICompletionProvider completionProvider;

    /// <inheritdoc />
    public override string Title => "Inference";

    /// <inheritdoc />
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.Settings, IconVariant = IconVariant.Filled };

    [ObservableProperty]
    private bool isPromptCompletionEnabled = true;

    [ObservableProperty]
    private IReadOnlyList<string> availableTagCompletionCsvs = Array.Empty<string>();

    [ObservableProperty]
    private string? selectedTagCompletionCsv;

    [ObservableProperty]
    private bool isCompletionRemoveUnderscoresEnabled = true;

    [ObservableProperty]
    [CustomValidation(typeof(InferenceSettingsViewModel), nameof(ValidateOutputImageFileNameFormat))]
    private string? outputImageFileNameFormat;

    [ObservableProperty]
    private string? outputImageFileNameFormatSample;

    [ObservableProperty]
    private bool isInferenceImageBrowserUseRecycleBinForDelete = true;

    [ObservableProperty]
    private bool filterExtraNetworksByBaseModel;

    [ObservableProperty] 
    private bool enableTiledVae;


    private List<string> ignoredFileNameFormatVars =
    [
        "author",
        "model_version_name",
        "base_model",
        "file_name",
        "model_type",
        "model_id",
        "model_version_id",
        "file_id",
    ];

    [ObservableProperty]
    public partial int InferenceDimensionStepChange { get; set; }

    [ObservableProperty]
    public partial ObservableHashSet<string> FavoriteDimensions { get; set; } = [];

    public IEnumerable<FileNameFormatVar> OutputImageFileNameFormatVars =>
        FileNameFormatProvider
            .GetSample()
            .Substitutions.Where(kv => !ignoredFileNameFormatVars.Contains(kv.Key))
            .Select(kv => new FileNameFormatVar { Variable = $"{{{kv.Key}}}", Example = kv.Value.Invoke() });

    [ObservableProperty]
    private bool isImageViewerPixelGridEnabled = true;

    public InferenceSettingsViewModel(
        INotificationService notificationService,
        IPrerequisiteHelper prerequisiteHelper,
        IPyRunner pyRunner,
        IServiceManager<ViewModelBase> dialogFactory,
        ICompletionProvider completionProvider,
        ITrackedDownloadService trackedDownloadService,
        IModelIndexService modelIndexService,
        INavigationService<SettingsViewModel> settingsNavigationService,
        IAccountsService accountsService,
        ISettingsManager settingsManager
    )
    {
        this.settingsManager = settingsManager;
        this.notificationService = notificationService;
        this.completionProvider = completionProvider;

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.SelectedTagCompletionCsv,
            settings => settings.TagCompletionCsv
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.IsPromptCompletionEnabled,
            settings => settings.IsPromptCompletionEnabled,
            true
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.IsCompletionRemoveUnderscoresEnabled,
            settings => settings.IsCompletionRemoveUnderscoresEnabled,
            true
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.IsInferenceImageBrowserUseRecycleBinForDelete,
            settings => settings.IsInferenceImageBrowserUseRecycleBinForDelete,
            true
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.FilterExtraNetworksByBaseModel,
            settings => settings.FilterExtraNetworksByBaseModel,
            true
        );

        this.WhenPropertyChanged(vm => vm.OutputImageFileNameFormat)
            .Throttle(TimeSpan.FromMilliseconds(50))
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(formatProperty =>
            {
                var provider = FileNameFormatProvider.GetSample();
                var template = formatProperty.Value ?? string.Empty;

                if (
                    !string.IsNullOrEmpty(template)
                    && provider.Validate(template) == ValidationResult.Success
                )
                {
                    var format = FileNameFormat.Parse(template, provider);
                    OutputImageFileNameFormatSample = format.GetFileName() + ".png";
                }
                else
                {
                    // Use default format if empty
                    var defaultFormat = FileNameFormat.Parse(FileNameFormat.DefaultTemplate, provider);
                    OutputImageFileNameFormatSample = defaultFormat.GetFileName() + ".png";
                }
            });

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.OutputImageFileNameFormat,
            settings => settings.InferenceOutputImageFileNameFormat,
            true
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.IsImageViewerPixelGridEnabled,
            settings => settings.IsImageViewerPixelGridEnabled,
            true
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.InferenceDimensionStepChange,
            settings => settings.InferenceDimensionStepChange,
            true
        );
        settingsManager.RelayPropertyFor(
            this,
            vm => vm.EnableTiledVae,
            settings => settings.EnableTiledVae,
            true
        );

        FavoriteDimensions
            .ToObservableChangeSet()
            .Throttle(TimeSpan.FromMilliseconds(50))
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(_ =>
            {
                if (
                    FavoriteDimensions is not { Count: > 0 }
                    || FavoriteDimensions.SetEquals(settingsManager.Settings.SavedInferenceDimensions)
                )
                    return;

                settingsManager.Transaction(s => s.SavedInferenceDimensions = FavoriteDimensions.ToHashSet());
            });

        ImportTagCsvCommand.WithNotificationErrorHandler(notificationService, LogLevel.Warn);
    }

    /// <summary>
    /// Validator for <see cref="OutputImageFileNameFormat"/>
    /// </summary>
    public static ValidationResult ValidateOutputImageFileNameFormat(
        string? format,
        ValidationContext context
    )
    {
        return FileNameFormatProvider.GetSample().Validate(format ?? string.Empty);
    }

    /// <inheritdoc />
    public override void OnLoaded()
    {
        base.OnLoaded();
        FavoriteDimensions.Clear();
        FavoriteDimensions.AddRange(
            settingsManager.Settings.SavedInferenceDimensions.OrderDescending(
                DimensionStringComparer.Instance
            )
        );

        UpdateAvailableTagCompletionCsvs();
    }

    #region Commands

    [RelayCommand(FlowExceptionsToTaskScheduler = true)]
    private async Task ImportTagCsv()
    {
        var storage = App.StorageProvider;
        var files = await storage.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                FileTypeFilter = new List<FilePickerFileType> { new("CSV") { Patterns = ["*.csv"] } },
            }
        );

        if (files.Count == 0)
            return;

        var sourceFile = new FilePath(files[0].TryGetLocalPath()!);

        var tagsDir = settingsManager.TagsDirectory;
        tagsDir.Create();

        // Copy to tags directory
        var targetFile = tagsDir.JoinFile(sourceFile.Name);
        await sourceFile.CopyToAsync(targetFile);

        // Update index
        UpdateAvailableTagCompletionCsvs();

        // Trigger load
        completionProvider.BackgroundLoadFromFile(targetFile, true);

        notificationService.Show(
            $"Imported {sourceFile.Name}",
            $"The {sourceFile.Name} file has been imported.",
            NotificationType.Success
        );
    }

    [RelayCommand]
    private async Task AddRow()
    {
        // FavoriteDimensions.Add(string.Empty);
        var textFields = new TextBoxField[]
        {
            new()
            {
                Label = "Width",
                Validator = text =>
                {
                    if (string.IsNullOrWhiteSpace(text))
                        throw new DataValidationException("Width is required");

                    if (!int.TryParse(text, out var width) || width <= 0)
                        throw new DataValidationException("Width must be a positive integer");
                },
                Watermark = "1024",
            },
            new()
            {
                Label = "Height",
                Validator = text =>
                {
                    if (string.IsNullOrWhiteSpace(text))
                        throw new DataValidationException("Height is required");

                    if (!int.TryParse(text, out var height) || height <= 0)
                        throw new DataValidationException("Height must be a positive integer");
                },
                Watermark = "1024",
            },
        };

        var dialog = DialogHelper.CreateTextEntryDialog("Add Favorite Dimensions", "", textFields);

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        var width = textFields[0].Text;
        var height = textFields[1].Text;

        if (string.IsNullOrWhiteSpace(width) || string.IsNullOrWhiteSpace(height))
            return;

        FavoriteDimensions.Add($"{width} x {height}");
    }

    [RelayCommand]
    private void RemoveSelectedRow(string item)
    {
        FavoriteDimensions.Remove(item);
    }

    #endregion

    private void UpdateAvailableTagCompletionCsvs()
    {
        if (!settingsManager.IsLibraryDirSet)
            return;

        if (settingsManager.TagsDirectory is not { Exists: true } tagsDir)
            return;

        var csvFiles = tagsDir.Info.EnumerateFiles("*.csv");
        AvailableTagCompletionCsvs = csvFiles.Select(f => f.Name).ToImmutableArray();

        // Set selected to current if exists
        var settingsCsv = settingsManager.Settings.TagCompletionCsv;
        if (settingsCsv is not null && AvailableTagCompletionCsvs.Contains(settingsCsv))
        {
            SelectedTagCompletionCsv = settingsCsv;
        }
    }
}
