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
    public partial ModelOrganizationPlan? Plan { get; set; }

    [ObservableProperty]
    public partial string OrganizePattern { get; set; } = FileNameFormat.DefaultOrganizationTemplate;

    [ObservableProperty]
    public partial string PatternPreviewSample { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? PatternValidationError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMissingMetadataWarning))]
    [NotifyPropertyChangedFor(nameof(ShowMetadataActions))]
    public partial int MissingMetadataCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowIncompleteMetadataWarning))]
    [NotifyPropertyChangedFor(nameof(ShowMetadataActions))]
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
    public partial int UnchangedCount { get; set; }

    public ObservableCollection<ModelOrganizationPreviewItem> Items { get; } = [];

    public bool CanOrganize => Plan?.ReadyCount > 0;

    public bool ShowMissingMetadataWarning => MissingMetadataCount > 0;

    public bool ShowIncompleteMetadataWarning => IncompleteMetadataCount > 0;

    public bool ShowMetadataActions => ShowMissingMetadataWarning || ShowIncompleteMetadataWarning;

    public string ReadySummary =>
        Plan == null
            ? string.Empty
            : $"{Plan.ReadyCount} ready, {Plan.ConflictCount} conflicts, {Plan.SkippedCount} skipped";

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
        RefreshVisibleItems();

        // Count models with no connected metadata at all
        MissingMetadataCount = plan.Items.Count(i =>
            i.Status == ModelOrganizationPreviewStatus.Skipped && !i.Model.HasConnectedModel
        );

        // Count models with connected metadata but missing fields needed by the template
        IncompleteMetadataCount = plan.Items.Count(i =>
            i.Status == ModelOrganizationPreviewStatus.Skipped
            && i.Model.HasConnectedModel
            && i.Reason?.Contains("not available", StringComparison.OrdinalIgnoreCase) == true
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

        if (!string.IsNullOrEmpty(template) && provider.Validate(template) == ValidationResult.Success)
        {
            var format = FileNameFormat.Parse(template, provider);
            PatternPreviewSample = "Example: " + format.GetFileName() + ".safetensors";
        }
        else
        {
            var defaultFormat = FileNameFormat.Parse(FileNameFormat.DefaultOrganizationTemplate, provider);
            PatternPreviewSample = "Example: " + defaultFormat.GetFileName() + ".safetensors";
        }
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
