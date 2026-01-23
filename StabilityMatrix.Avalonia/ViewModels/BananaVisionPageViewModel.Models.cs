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
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.ViewModels;

public partial class BananaVisionPageViewModel
{
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
            var baseModel = model.Local?.ConnectedModelInfo?.BaseModel;

            // Check primary terms first
            if (
                primaryTerms.Any(term =>
                    baseModel?.Contains(term, StringComparison.OrdinalIgnoreCase) == true
                )
            )
            {
                primaryModels.Add(model);
            }
            // Check secondary terms
            else if (
                secondaryTerms?.Any(term =>
                    baseModel?.Contains(term, StringComparison.OrdinalIgnoreCase) == true
                ) == true
            )
            {
                secondaryModels.Add(model);
            }
            // Check filename fallback for untagged models
            else if (string.IsNullOrEmpty(baseModel))
            {
                if (
                    primaryTerms.Any(term =>
                        model.FileName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    )
                )
                {
                    primaryModels.Add(model);
                }
                else
                {
                    untaggedModels.Add(model);
                }
            }
        }

        return (primaryModels, secondaryModels, untaggedModels);
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

    [RelayCommand]
    private async Task AddLoraAsync()
    {
        // Get available LoRAs based on current provider
        var availableLoras =
            SelectedProviderId == BananaVisionProviderIds.QwenImageEdit
                ? AvailableQwenLoras
                : AvailableFluxLoras;

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
}
