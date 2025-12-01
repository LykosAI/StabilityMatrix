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
    /// Loads available Flux Kontext models from the DiffusionModels folder using local model index
    /// </summary>
    private void LoadAvailableFluxModels()
    {
        // Categorize UNet models
        var kontextModels = new List<HybridModelFile>();
        var untaggedModels = new List<HybridModelFile>();

        foreach (
            var model in modelIndexService
                .FindByModelType(SharedFolderType.DiffusionModels)
                .Select(HybridModelFile.FromLocal)
        )
        {
            var baseModel = model.Local?.ConnectedModelInfo?.BaseModel;

            if (baseModel?.Contains("Kontext", StringComparison.OrdinalIgnoreCase) == true)
            {
                kontextModels.Add(model);
            }
            else if (string.IsNullOrEmpty(baseModel))
            {
                // Also check filename for "kontext" as fallback
                if (model.FileName.Contains("kontext", StringComparison.OrdinalIgnoreCase))
                {
                    kontextModels.Add(model);
                }
                else
                {
                    untaggedModels.Add(model);
                }
            }
        }

        PopulateModelCollection(AvailableFluxModels, kontextModels, untaggedModels);

        // Auto-select first Kontext model if available
        if (SelectedFluxModel == null && AvailableFluxModels.Count > 0)
        {
            SelectedFluxModel =
                AvailableFluxModels.FirstOrDefault(m =>
                    m.FileName.Contains("kontext", StringComparison.OrdinalIgnoreCase)
                ) ?? AvailableFluxModels.First();
        }

        // Categorize LoRA models - prioritize Flux Kontext, then Flux, then untagged
        var kontextLoras = new List<HybridModelFile>();
        var fluxLoras = new List<HybridModelFile>();
        var untaggedLoras = new List<HybridModelFile>();

        foreach (
            var lora in modelIndexService
                .FindByModelType(SharedFolderType.Lora | SharedFolderType.LyCORIS)
                .Select(HybridModelFile.FromLocal)
        )
        {
            var baseModel = lora.Local?.ConnectedModelInfo?.BaseModel;

            if (baseModel?.Contains("Kontext", StringComparison.OrdinalIgnoreCase) == true)
            {
                kontextLoras.Add(lora);
            }
            else if (baseModel?.Contains("Flux", StringComparison.OrdinalIgnoreCase) == true)
            {
                fluxLoras.Add(lora);
            }
            else if (string.IsNullOrEmpty(baseModel))
            {
                untaggedLoras.Add(lora);
            }
        }

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
        // Categorize UNet models
        var qwenModels = new List<HybridModelFile>();
        var untaggedModels = new List<HybridModelFile>();

        foreach (
            var model in modelIndexService
                .FindByModelType(SharedFolderType.DiffusionModels)
                .Select(HybridModelFile.FromLocal)
        )
        {
            var baseModel = model.Local?.ConnectedModelInfo?.BaseModel;

            if (baseModel?.Contains("Qwen", StringComparison.OrdinalIgnoreCase) == true)
            {
                qwenModels.Add(model);
            }
            else if (string.IsNullOrEmpty(baseModel))
            {
                // Also check filename for "qwen" as fallback
                if (model.FileName.Contains("qwen", StringComparison.OrdinalIgnoreCase))
                {
                    qwenModels.Add(model);
                }
                else
                {
                    untaggedModels.Add(model);
                }
            }
        }

        PopulateModelCollection(AvailableQwenModels, qwenModels, untaggedModels);

        // Auto-select first Qwen model if available
        if (SelectedQwenModel == null && AvailableQwenModels.Count > 0)
        {
            SelectedQwenModel =
                AvailableQwenModels.FirstOrDefault(m =>
                    m.FileName.Contains("qwen", StringComparison.OrdinalIgnoreCase)
                ) ?? AvailableQwenModels.First();
        }

        // Load all LoRA models (all are potentially compatible with Qwen)
        var allLoras = modelIndexService
            .FindByModelType(SharedFolderType.Lora | SharedFolderType.LyCORIS)
            .Select(HybridModelFile.FromLocal)
            .ToList();

        PopulateModelCollection(AvailableQwenLoras, allLoras);

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
