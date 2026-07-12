using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Layout;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.BananaVision;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.ViewModels;

internal enum BananaVisionModelTermMatch
{
    Excluded,
    Primary,
    Secondary,
    Untagged,
}

public partial class BananaVisionPageViewModel
{
    /// <summary>
    /// Provider-aware availability check. Klein's check is encoder-variant aware (a 9B UNET
    /// needs the 8B encoder), so the user's UNET selection is passed through for it.
    /// </summary>
    private bool AreProviderModelsAvailable(ILocalProviderModelManager manager) =>
        manager is Flux2KleinModelManager klein
            ? klein.AreModelsAvailable(ClientManager, SelectedKleinModel)
            : manager.AreModelsAvailable(ClientManager);

    /// <summary>
    /// Provider-aware missing-model list. See <see cref="AreProviderModelsAvailable"/>.
    /// </summary>
    private IEnumerable<RemoteResource> GetProviderMissingModels(ILocalProviderModelManager manager) =>
        manager is Flux2KleinModelManager klein
            ? klein.GetMissingModels(ClientManager, SelectedKleinModel)
            : manager.GetMissingModels(ClientManager);

    /// <summary>
    /// Provider-aware missing-model display names. See <see cref="AreProviderModelsAvailable"/>.
    /// </summary>
    private IEnumerable<string> GetProviderMissingModelNames(ILocalProviderModelManager manager) =>
        manager is Flux2KleinModelManager klein
            ? klein.GetMissingModelNames(ClientManager, SelectedKleinModel)
            : manager.GetMissingModelNames(ClientManager);

    /// <summary>
    /// Sorts models by connected status first, then alphabetically by display name
    /// </summary>
    private static IOrderedEnumerable<HybridModelFile> SortModelsByConnectedThenName(
        IEnumerable<HybridModelFile> models
    )
    {
        return models
            .OrderByDescending(m => m.Local?.ConnectedModelInfo != null)
            .ThenBy(m => m.Local?.DisplayModelName ?? m.ShortDisplayName);
    }

    /// <summary>
    /// Populates a collection with sorted models from multiple priority groups
    /// </summary>
    private static void PopulateModelCollection(
        ObservableCollection<HybridModelFile> collection,
        params IEnumerable<HybridModelFile>[] modelGroups
    )
    {
        collection.Clear();
        foreach (var group in modelGroups)
        {
            foreach (var model in SortModelsByConnectedThenName(group))
            {
                collection.Add(model);
            }
        }
    }

    /// <summary>
    /// Categorizes models from a folder type based on search terms
    /// </summary>
    /// <param name="folderType">The folder type to search</param>
    /// <param name="primaryTerms">Primary search terms (highest priority)</param>
    /// <param name="secondaryTerms">Secondary search terms (medium priority, optional)</param>
    /// <returns>Tuple of (matched models, secondary matched models, untagged models)</returns>
    private (
        List<HybridModelFile> Primary,
        List<HybridModelFile> Secondary,
        List<HybridModelFile> Untagged
    ) CategorizeModelsByTerms(
        SharedFolderType folderType,
        string[] primaryTerms,
        string[]? secondaryTerms = null
    )
    {
        var primaryModels = new List<HybridModelFile>();
        var secondaryModels = new List<HybridModelFile>();
        var untaggedModels = new List<HybridModelFile>();

        foreach (var model in modelIndexService.FindByModelType(folderType).Select(HybridModelFile.FromLocal))
        {
            switch (GetModelTermMatch(model, primaryTerms, secondaryTerms))
            {
                case BananaVisionModelTermMatch.Primary:
                    primaryModels.Add(model);
                    break;
                case BananaVisionModelTermMatch.Secondary:
                    secondaryModels.Add(model);
                    break;
                case BananaVisionModelTermMatch.Untagged:
                    untaggedModels.Add(model);
                    break;
            }
        }

        return (primaryModels, secondaryModels, untaggedModels);
    }

    internal static BananaVisionModelTermMatch GetModelTermMatch(
        HybridModelFile model,
        string[] primaryTerms,
        string[]? secondaryTerms = null
    )
    {
        var baseModel = model.Local?.ConnectedModelInfo?.BaseModel;

        if (primaryTerms.Any(term => baseModel?.Contains(term, StringComparison.OrdinalIgnoreCase) == true))
        {
            return BananaVisionModelTermMatch.Primary;
        }

        if (
            secondaryTerms?.Any(term => baseModel?.Contains(term, StringComparison.OrdinalIgnoreCase) == true)
            == true
        )
        {
            return BananaVisionModelTermMatch.Secondary;
        }

        // Filename fallback applies even when metadata is present but unrecognized, which
        // is common for CivitAI uploads tagged "Other".
        if (primaryTerms.Any(term => model.FileName.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return BananaVisionModelTermMatch.Primary;
        }

        if (
            secondaryTerms?.Any(term => model.FileName.Contains(term, StringComparison.OrdinalIgnoreCase))
            == true
        )
        {
            return BananaVisionModelTermMatch.Secondary;
        }

        return string.IsNullOrEmpty(baseModel)
            ? BananaVisionModelTermMatch.Untagged
            : BananaVisionModelTermMatch.Excluded;
    }

    /// <summary>
    /// Loads available Flux Kontext models from the DiffusionModels folder using local model index
    /// </summary>
    private void LoadAvailableFluxModels()
    {
        // Load UNet models - prioritize Kontext
        var (kontextModels, _, untaggedModels) = CategorizeModelsByTerms(
            SharedFolderType.DiffusionModels,
            ["Kontext"]
        );

        PopulateModelCollection(AvailableFluxModels, kontextModels, untaggedModels);

        // Auto-select first Kontext model if available
        if (SelectedFluxModel == null && AvailableFluxModels.Count > 0)
        {
            SelectedFluxModel =
                AvailableFluxModels.FirstOrDefault(m =>
                    m.FileName.Contains("kontext", StringComparison.OrdinalIgnoreCase)
                ) ?? AvailableFluxModels.First();
        }

        // Load LoRA models - prioritize Kontext, then Flux, then untagged
        var (kontextLoras, fluxLoras, untaggedLoras) = CategorizeModelsByTerms(
            SharedFolderType.Lora | SharedFolderType.LyCORIS,
            ["Kontext"],
            ["Flux"]
        );

        PopulateModelCollection(AvailableFluxLoras, kontextLoras, fluxLoras, untaggedLoras);

        logger.LogInformation(
            "Loaded {ModelCount} Flux models and {LoraCount} LoRAs from local index",
            AvailableFluxModels.Count,
            AvailableFluxLoras.Count
        );
    }

    /// <summary>
    /// Loads available Qwen Image Edit models from the DiffusionModels folder using local model index
    /// </summary>
    private void LoadAvailableQwenModels()
    {
        // Load UNet models - prioritize Qwen
        var (qwenModels, _, untaggedModels) = CategorizeModelsByTerms(
            SharedFolderType.DiffusionModels,
            ["Qwen"]
        );

        PopulateModelCollection(AvailableQwenModels, qwenModels, untaggedModels);

        // Auto-select first Qwen model if available
        if (SelectedQwenModel == null && AvailableQwenModels.Count > 0)
        {
            SelectedQwenModel =
                AvailableQwenModels.FirstOrDefault(m =>
                    m.FileName.Contains("qwen", StringComparison.OrdinalIgnoreCase)
                ) ?? AvailableQwenModels.First();
        }

        // Load LoRA models - prioritize Qwen, then untagged
        var (qwenLoras, _, untaggedLoras) = CategorizeModelsByTerms(
            SharedFolderType.Lora | SharedFolderType.LyCORIS,
            ["Qwen"]
        );

        PopulateModelCollection(AvailableQwenLoras, qwenLoras, untaggedLoras);

        logger.LogInformation(
            "Loaded {ModelCount} Qwen models and {LoraCount} LoRAs from local index",
            AvailableQwenModels.Count,
            AvailableQwenLoras.Count
        );
    }

    /// <summary>
    /// Loads available Flux.2 Klein models from the DiffusionModels folder using local model index.
    /// Picks up both Klein 4B and Klein 9B variants for the dropdown selector.
    /// </summary>
    private void LoadAvailableKleinModels()
    {
        // Load UNet models - prioritize Klein, then any Flux.2 (catches future variants), then untagged
        var (kleinModels, flux2Models, untaggedModels) = CategorizeModelsByTerms(
            SharedFolderType.DiffusionModels,
            ["Klein", "flux-2-klein", "flux2-klein"],
            ["Flux.2", "flux2"]
        );

        PopulateModelCollection(AvailableKleinModels, kleinModels, flux2Models, untaggedModels);

        // Auto-select first Klein model if available — prefer 4B since it's the auto-downloaded
        // Apache 2.0 default, then any other Klein variant the user has dropped in
        if (SelectedKleinModel == null && AvailableKleinModels.Count > 0)
        {
            SelectedKleinModel =
                AvailableKleinModels.FirstOrDefault(m =>
                    m.FileName.Contains("klein-4b", StringComparison.OrdinalIgnoreCase)
                    || m.FileName.Contains("klein_4b", StringComparison.OrdinalIgnoreCase)
                )
                ?? AvailableKleinModels.FirstOrDefault(m =>
                    m.FileName.Contains("klein", StringComparison.OrdinalIgnoreCase)
                )
                ?? AvailableKleinModels.First();
        }

        // Load LoRA models - prioritize Klein, then any Flux LoRA, then untagged
        var (kleinLoras, fluxLoras, untaggedLoras) = CategorizeModelsByTerms(
            SharedFolderType.Lora | SharedFolderType.LyCORIS,
            ["Klein", "Flux.2"],
            ["Flux"]
        );

        PopulateModelCollection(AvailableKleinLoras, kleinLoras, fluxLoras, untaggedLoras);

        logger.LogInformation(
            "Loaded {ModelCount} Klein models and {LoraCount} LoRAs from local index",
            AvailableKleinModels.Count,
            AvailableKleinLoras.Count
        );
    }

    [RelayCommand]
    private async Task AddLoraAsync()
    {
        // Get available LoRAs based on current provider
        var availableLoras = SelectedProviderId switch
        {
            BananaVisionProviderIds.QwenImageEdit => AvailableQwenLoras,
            BananaVisionProviderIds.Flux2Klein => AvailableKleinLoras,
            _ => AvailableFluxLoras,
        };

        if (availableLoras.Count == 0)
        {
            notificationService.Show(
                "No LoRAs Available",
                "No compatible LoRA models found.",
                NotificationType.Warning
            );
            return;
        }

        // Create a styled selection dialog using BetterComboBox with HybridModel theme
        var comboBox = new BetterComboBox
        {
            ItemsSource = availableLoras,
            SelectedIndex = 0,
            MinWidth = 350,
            Padding = new Thickness(8, 6, 4, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        // Apply the HybridModel theme
        if (
            App.Current?.Resources.TryGetResource(
                "BetterComboBoxHybridModelTheme",
                App.Current.ActualThemeVariant,
                out var theme
            ) == true
            && theme is ControlTheme controlTheme
        )
        {
            comboBox.Theme = controlTheme;
        }

        var dialog = new ContentDialog
        {
            Title = "Add LoRA",
            Content = comboBox,
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && comboBox.SelectedItem is HybridModelFile selectedLora)
        {
            // Check if already added
            if (SelectedLoras.Any(l => l.Model.RelativePath == selectedLora.RelativePath))
            {
                notificationService.Show(
                    "Already Added",
                    "This LoRA is already in the list.",
                    NotificationType.Warning
                );
                return;
            }

            SelectedLoras.Add(new SelectedLora { Model = selectedLora });
        }
    }

    [RelayCommand]
    private void RemoveLora(SelectedLora lora)
    {
        SelectedLoras.Remove(lora);
    }

    [RelayCommand]
    private void ToggleFluxSettings()
    {
        IsFluxSettingsExpanded = !IsFluxSettingsExpanded;
    }

    [RelayCommand]
    private void ToggleQwenSettings()
    {
        IsQwenSettingsExpanded = !IsQwenSettingsExpanded;
    }

    [RelayCommand]
    private void ToggleKleinSettings()
    {
        IsKleinSettingsExpanded = !IsKleinSettingsExpanded;
    }

    /// <summary>
    /// When the user picks a different Klein model, snap Steps/CFG to the recommended
    /// defaults for that variant. Distilled = 4 steps / CFG 1, Base = 20 steps / CFG 5.
    /// The user can still override afterwards; this just sets sane starting values.
    /// </summary>
    partial void OnSelectedKleinModelChanged(HybridModelFile? value)
    {
        if (value == null)
            return;

        var (recommendedSteps, recommendedCfg) = DetectKleinDefaults(value);
        KleinSteps = recommendedSteps;
        KleinCfg = recommendedCfg;

        // The availability check is encoder-variant aware, so switching between a 4B and a
        // 9B UNET can change which text encoder counts as missing — re-evaluate the banner.
        UpdateProviderStatus();
    }

    /// <summary>
    /// Returns the recommended Steps and CFG for a Klein UNET, based on filename and
    /// CivitAI metadata. Base variants need 20 steps / CFG 5; distilled needs 4 / 1.
    /// 9B models without an explicit "distilled" tag are assumed to be base, since
    /// Klein 9B distilled isn't publicly shipped — almost all 9B installs are base
    /// (or fine-tunes of base). 4B without signals defaults to distilled, matching
    /// our auto-downloaded Apache 2.0 default.
    /// </summary>
    private static (int Steps, double Cfg) DetectKleinDefaults(HybridModelFile model)
    {
        var info = model.Local?.ConnectedModelInfo;

        var haystacks = new List<string> { model.FileName };
        if (info != null)
        {
            if (!string.IsNullOrEmpty(info.BaseModel))
                haystacks.Add(info.BaseModel);
            if (!string.IsNullOrEmpty(info.ModelName))
                haystacks.Add(info.ModelName);
            if (!string.IsNullOrEmpty(info.VersionName))
                haystacks.Add(info.VersionName);
            if (!string.IsNullOrEmpty(info.VersionDescription))
                haystacks.Add(info.VersionDescription);
            if (info.TrainedWords != null)
                haystacks.AddRange(info.TrainedWords);
        }

        bool LooksLikeBase(string s) =>
            s.Contains("base", StringComparison.OrdinalIgnoreCase)
            || s.Contains("non-distilled", StringComparison.OrdinalIgnoreCase)
            || s.Contains("non_distilled", StringComparison.OrdinalIgnoreCase)
            || s.Contains("nondistilled", StringComparison.OrdinalIgnoreCase)
            || s.Contains("foundation", StringComparison.OrdinalIgnoreCase);

        bool LooksLikeDistilled(string s) =>
            s.Contains("distilled", StringComparison.OrdinalIgnoreCase)
            || s.Contains("turbo", StringComparison.OrdinalIgnoreCase);

        bool LooksLikeNineB(string s) =>
            s.Contains("9b", StringComparison.OrdinalIgnoreCase)
            || s.Contains("9 b", StringComparison.OrdinalIgnoreCase)
            || s.Contains("9-b", StringComparison.OrdinalIgnoreCase)
            || s.Contains("klein 9", StringComparison.OrdinalIgnoreCase)
            || s.Contains("klein-9", StringComparison.OrdinalIgnoreCase)
            || s.Contains("klein_9", StringComparison.OrdinalIgnoreCase);

        var hasBaseSignal = haystacks.Any(LooksLikeBase);
        var hasDistilledSignal = haystacks.Any(LooksLikeDistilled);
        var hasNineBSignal = haystacks.Any(LooksLikeNineB);

        // Ambiguous case: BOTH "base" and "distilled" appear (common for community uploads
        // labeled e.g. "Klein 9B Base & Distilled" that cover both variants). Prefer base
        // for 9B (distilled 9B isn't publicly shipped) and distilled for 4B (matches our
        // auto-download default).
        if (hasBaseSignal && hasDistilledSignal)
            return hasNineBSignal ? (20, 5.0) : (4, 1.0);

        // Unambiguous explicit tags.
        if (hasDistilledSignal)
            return (4, 1.0);
        if (hasBaseSignal)
            return (20, 5.0);

        // No explicit base/distilled signal, but it's a 9B variant — default to base.
        // Klein 9B distilled isn't publicly shipped, so 9B installs (including merges and
        // fine-tunes) are almost always base-derived and need 20 steps / CFG 5.
        if (hasNineBSignal)
            return (20, 5.0);

        // Default: distilled (matches the auto-downloaded Apache 2.0 Klein 4B).
        return (4, 1.0);
    }
}
