using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

/// <summary>
/// Assigns categories to one or more models and lets the user add, delete, reorder (by dragging
/// a row) or recolour categories. Every change is applied immediately, so the dialog only has a
/// Close button. With no <see cref="Targets"/> it acts as a plain category manager.
/// </summary>
[View(typeof(ModelCategoriesDialog))]
[ManagedService]
[RegisterTransient<ModelCategoriesDialogViewModel>]
public partial class ModelCategoriesDialogViewModel(
    IModelCategoryService categoryService,
    IModelIndexService modelIndexService,
    INotificationService notificationService
) : ContentDialogViewModelBase
{
    private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

    private bool isLoadingRows;

    /// <summary>
    /// Models the checkboxes act on. Re-resolved from the index after every write so later
    /// toggles see the current categories.
    /// </summary>
    public IReadOnlyList<LocalModelFile> Targets { get; set; } = [];

    public ObservableCollection<ModelCategoryRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCategoryCommand))]
    public partial string NewCategoryName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasRows { get; set; }

    public bool HasTargets => Targets.Count > 0;

    public string TargetsDescription =>
        Targets.Count switch
        {
            0 => string.Empty,
            1 => string.Format(Resources.Label_AppliesToModel, Targets[0].DisplayModelName),
            var n => string.Format(Resources.Label_AppliesToModels, n),
        };

    public override void OnLoaded()
    {
        base.OnLoaded();
        Rows.CollectionChanged += OnRowsCollectionChanged;
        LoadRows();
    }

    public override void OnUnloaded()
    {
        base.OnUnloaded();
        Rows.CollectionChanged -= OnRowsCollectionChanged;
    }

    public override BetterContentDialog GetDialog()
    {
        var dialog = base.GetDialog();
        dialog.Title = HasTargets ? Resources.Label_SetCategories : Resources.Label_ManageCategories;
        dialog.MinDialogWidth = 440;
        dialog.MaxDialogHeight = 640;
        dialog.IsFooterVisible = true;
        dialog.CloseButtonText = Resources.Action_Close;
        dialog.DefaultButton = ContentDialogButton.Close;
        return dialog;
    }

    private void LoadRows()
    {
        isLoadingRows = true;
        try
        {
            foreach (var row in Rows)
            {
                row.PropertyChanged -= OnRowPropertyChanged;
            }
            Rows.Clear();

            var allModels = modelIndexService.ModelIndex.Values.SelectMany(models => models).ToList();

            foreach (var category in categoryService.GetCategories())
            {
                var row = new ModelCategoryRowViewModel
                {
                    Name = category.Name,
                    Count = allModels.Count(m => m.Categories.Contains(category.Name, NameComparer)),
                    IsChecked = GetCheckedState(category.Name),
                    Color = ModelCategoryStyle.ParseColor(category.Color),
                };
                row.PropertyChanged += OnRowPropertyChanged;
                Rows.Add(row);
            }

            HasRows = Rows.Count > 0;
        }
        finally
        {
            isLoadingRows = false;
        }
    }

    private bool? GetCheckedState(string category)
    {
        if (!HasTargets)
        {
            return false;
        }

        var have = Targets.Count(t => t.Categories.Contains(category, NameComparer));
        return have == 0 ? false
            : have == Targets.Count ? true
            : null;
    }

    private async void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (isLoadingRows || sender is not ModelCategoryRowViewModel row)
        {
            return;
        }

        try
        {
            switch (e.PropertyName)
            {
                case nameof(ModelCategoryRowViewModel.IsChecked) when HasTargets:
                    // Three-state checkbox: a click on the mixed state lands on true
                    if (row.IsChecked is null)
                    {
                        row.IsChecked = true;
                        return;
                    }

                    if (row.IsChecked == true)
                    {
                        await categoryService.AddToCategoryAsync(Targets, row.Name);
                    }
                    else
                    {
                        await categoryService.RemoveFromCategoryAsync(Targets, row.Name);
                    }

                    RefreshTargets();
                    row.Count = modelIndexService
                        .ModelIndex.Values.SelectMany(models => models)
                        .Count(m => m.Categories.Contains(row.Name, NameComparer));
                    break;

                case nameof(ModelCategoryRowViewModel.Color):
                    categoryService.SetCategoryColor(row.Name, ModelCategoryStyle.ToHex(row.Color));
                    break;
            }
        }
        catch (Exception ex)
        {
            notificationService.Show(Resources.Label_Categories, ex.Message, NotificationType.Error);
            LoadRows();
        }
    }

    /// <summary>
    /// A drag reorder moves a row as a remove followed by an insert; the insert completes the
    /// move, so that is when the new order is saved.
    /// </summary>
    private void OnRowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (
            isLoadingRows
            || e.Action is not (NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Move)
        )
        {
            return;
        }

        try
        {
            categoryService.SetCategoryOrder(Rows.Select(r => r.Name));
        }
        catch (Exception ex)
        {
            notificationService.Show(Resources.Label_Categories, ex.Message, NotificationType.Error);
        }
    }

    [RelayCommand]
    private async Task DeleteCategory(ModelCategoryRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        try
        {
            await categoryService.DeleteCategoryAsync(row.Name);
            RefreshTargets();
        }
        catch (Exception ex)
        {
            notificationService.Show(Resources.Label_Categories, ex.Message, NotificationType.Error);
        }

        LoadRows();
    }

    [RelayCommand(CanExecute = nameof(CanAddCategory))]
    private async Task AddCategory()
    {
        var name = NewCategoryName.Trim();

        try
        {
            var color = ModelCategoryStyle.NextPaletteColor(categoryService.GetCategories());
            categoryService.AddCategory(name, ModelCategoryStyle.ToHex(color));

            if (HasTargets)
            {
                await categoryService.AddToCategoryAsync(Targets, name);
                RefreshTargets();
            }
        }
        catch (Exception ex)
        {
            notificationService.Show(Resources.Label_Categories, ex.Message, NotificationType.Error);
        }

        NewCategoryName = string.Empty;
        LoadRows();
    }

    private bool CanAddCategory() => !string.IsNullOrWhiteSpace(NewCategoryName);

    private void RefreshTargets()
    {
        var byPath = modelIndexService
            .ModelIndex.Values.SelectMany(models => models)
            .ToDictionary(m => m.RelativePath, m => m);

        Targets = Targets.Select(t => byPath.GetValueOrDefault(t.RelativePath, t)).ToList();
    }
}
