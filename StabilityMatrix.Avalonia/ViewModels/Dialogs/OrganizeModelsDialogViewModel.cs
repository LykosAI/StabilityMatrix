using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Controls.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models.CheckpointOrganizer;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(OrganizeModelsDialog))]
[ManagedService]
[RegisterTransient<OrganizeModelsDialogViewModel>]
public partial class OrganizeModelsDialogViewModel(
    ISettingsManager settingsManager,
    ModelOrganizationService modelOrganizationService
) : ContentDialogViewModelBase
{
    private IReadOnlyList<LocalModelFile> models = [];
    private string modelsRoot = string.Empty;
    private string scopePath = string.Empty;
    private bool includeNested;
    private IReadOnlyList<ModelOrganizationPreviewItem> allSortedItems = [];

    public ModelOrganizationMetadataAction RequestedMetadataAction { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOrganize))]
    [NotifyPropertyChangedFor(nameof(ReadySummary))]
    [NotifyPropertyChangedFor(nameof(ShowPatternHint))]
    public partial ModelOrganizationPlan? Plan { get; set; }

    [ObservableProperty]
    public partial string OrganizePattern { get; set; } = FileNameFormat.DefaultOrganizationTemplate;

    [ObservableProperty]
    public partial string PatternPreviewSample { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPatternHint))]
    public partial string? PatternValidationError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMissingMetadataWarning))]
    [NotifyPropertyChangedFor(nameof(ShowMetadataActions))]
    [NotifyPropertyChangedFor(nameof(MissingMetadataText))]
    public partial int MissingMetadataCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowIncompleteMetadataWarning))]
    [NotifyPropertyChangedFor(nameof(ShowMetadataActions))]
    [NotifyPropertyChangedFor(nameof(IncompleteMetadataText))]
    public partial int IncompleteMetadataCount { get; set; }

    [ObservableProperty]
    public partial bool IsVariablesTipOpen { get; set; }

    [ObservableProperty]
    public partial bool ShowReadyItems { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowConflictItems { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowSkippedItems { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowUnchangedItems { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUnchangedLabel))]
    [NotifyPropertyChangedFor(nameof(ShowPatternHint))]
    public partial int UnchangedCount { get; set; }

    public ObservableCollection<ModelOrganizationPreviewItem> Items { get; } = [];

    public bool CanOrganize => Plan?.ReadyCount > 0;

    /// <summary>
    /// True when the pattern is valid but moves nothing - the out-of-box state with the
    /// default single-segment pattern - so the dialog can explain instead of sitting inert.
    /// </summary>
    public bool ShowPatternHint =>
        PatternValidationError == null && Plan is { ReadyCount: 0, ConflictCount: 0 } && UnchangedCount > 0;

    public string PatternHintText =>
        string.Format(Resources.Text_OrganizePatternHint, FileNameFormat.SuggestedOrganizationTemplate);

    public string UseSuggestedPatternLabel =>
        string.Format(Resources.Action_UsePattern, FileNameFormat.SuggestedOrganizationTemplate);

    public bool ShowMissingMetadataWarning => MissingMetadataCount > 0;

    public bool ShowIncompleteMetadataWarning => IncompleteMetadataCount > 0;

    public bool ShowMetadataActions => ShowMissingMetadataWarning || ShowIncompleteMetadataWarning;

    public string ReadySummary =>
        Plan == null
            ? string.Empty
            : $"{Plan.ReadyCount} ready, {Plan.ConflictCount} conflicts, {Plan.SkippedCount} skipped";

    public string MissingMetadataText =>
        string.Format(Resources.TextTemplate_FilesNeedMetadata, MissingMetadataCount);

    public string IncompleteMetadataText =>
        string.Format(Resources.TextTemplate_FilesIncompleteMetadata, IncompleteMetadataCount);

    public string ShowUnchangedLabel =>
        string.Format(Resources.TextTemplate_ShowUnchangedCount, UnchangedCount);

    public IEnumerable<FileNameFormatVar> OrganizationFormatVars =>
        FileNameFormatProvider
            .GetSampleForOrganization()
            .Substitutions.Where(kv => FileNameFormatProvider.LocalOrganizationVariables.Contains(kv.Key))
            .Select(kv => new FileNameFormatVar { Variable = $"{{{kv.Key}}}", Example = kv.Value.Invoke() });

    public void Initialize(
        IEnumerable<LocalModelFile> allModels,
        string rootPath,
        string scope,
        bool nested,
        string? initialPattern
    )
    {
        models = allModels.ToList();
        modelsRoot = rootPath;
        scopePath = scope;
        includeNested = nested;
        RequestedMetadataAction = ModelOrganizationMetadataAction.None;

        OrganizePattern = string.IsNullOrWhiteSpace(initialPattern)
            ? FileNameFormat.DefaultOrganizationTemplate
            : initialPattern;

        RebuildPlan();

        AddDisposable(
            this.WhenPropertyChanged(vm => vm.OrganizePattern)
                .Throttle(TimeSpan.FromMilliseconds(300))
                .ObserveOn(SynchronizationContext.Current!)
                .Subscribe(_ => RebuildPlan())
        );
    }

    private void RebuildPlan()
    {
        var plan = modelOrganizationService.BuildPlan(
            models,
            modelsRoot,
            scopePath,
            includeNested,
            OrganizePattern
        );

        Plan = plan;
        PatternValidationError = plan.ValidationError;

        // Update preview sample
        UpdatePreviewSample();

        // Store all sorted items and update visible items
        allSortedItems = plan.Items.OrderBy(i => i.SortOrder).ToList();
        UnchangedCount = plan.Items.Count(i => i.IsUnchanged);

        // A plan where nothing moves would otherwise render an empty list with a disabled
        // button - reveal the unchanged items so the user can see what was evaluated
        if (!ShowUnchangedItems && allSortedItems.Count > 0 && allSortedItems.All(i => i.IsUnchanged))
        {
            ShowUnchangedItems = true;
        }

        RefreshVisibleItems();

        MissingMetadataCount = plan.Items.Count(i =>
            i.SkipReason == ModelOrganizationSkipReason.MissingMetadata
        );

        IncompleteMetadataCount = plan.Items.Count(i =>
            i.SkipReason == ModelOrganizationSkipReason.VariableUnavailable
        );
    }

    partial void OnShowReadyItemsChanged(bool value) => RefreshVisibleItems();

    partial void OnShowConflictItemsChanged(bool value) => RefreshVisibleItems();

    partial void OnShowSkippedItemsChanged(bool value) => RefreshVisibleItems();

    partial void OnShowUnchangedItemsChanged(bool value) => RefreshVisibleItems();

    private void RefreshVisibleItems()
    {
        Items.Clear();
        foreach (var item in allSortedItems)
        {
            var visible = item.Status switch
            {
                ModelOrganizationPreviewStatus.Ready => ShowReadyItems,
                ModelOrganizationPreviewStatus.Conflict => ShowConflictItems,
                ModelOrganizationPreviewStatus.Skipped => ShowSkippedItems,
                ModelOrganizationPreviewStatus.Unchanged => ShowUnchangedItems,
                _ => true,
            };

            if (visible)
            {
                Items.Add(item);
            }
        }
    }

    private void UpdatePreviewSample()
    {
        var provider = FileNameFormatProvider.GetSampleForOrganization();
        var template = OrganizePattern;

        var format =
            !string.IsNullOrEmpty(template) && provider.Validate(template) == ValidationResult.Success
                ? FileNameFormat.Parse(template, provider)
                : FileNameFormat.Parse(FileNameFormat.DefaultOrganizationTemplate, provider);

        PatternPreviewSample = string.Format(
            Resources.TextTemplate_PatternPreviewExample,
            format.GetFileName() + ".safetensors"
        );
    }

    public override BetterContentDialog GetDialog()
    {
        var dialog = base.GetDialog();
        dialog.MinDialogWidth = 1120;
        dialog.MaxDialogHeight = 900;
        dialog.IsFooterVisible = false;
        dialog.CloseOnClickOutside = true;
        dialog.ContentVerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        return dialog;
    }

    [RelayCommand]
    private void UseSuggestedPattern() => OrganizePattern = FileNameFormat.SuggestedOrganizationTemplate;

    [RelayCommand]
    private void OpenVariablesTip() => IsVariablesTipOpen = true;

    [RelayCommand]
    private void ToggleVariablesTip() => IsVariablesTipOpen = !IsVariablesTipOpen;

    [RelayCommand(CanExecute = nameof(CanOrganize))]
    private void ConfirmOrganize()
    {
        settingsManager.Transaction(s => s.ModelOrganizationFileNamePattern = OrganizePattern);
        OnPrimaryButtonClick();
    }

    [RelayCommand]
    private void ScanForMetadata()
    {
        RequestedMetadataAction = ModelOrganizationMetadataAction.ScanMissing;
        OnSecondaryButtonClick();
    }

    [RelayCommand]
    private void UpdateMetadata()
    {
        RequestedMetadataAction = ModelOrganizationMetadataAction.UpdateExisting;
        OnSecondaryButtonClick();
    }
}
