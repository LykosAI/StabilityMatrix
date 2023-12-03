using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using DynamicData.Binding;
using StabilityMatrix.Avalonia.Models.HuggingFace;
using StabilityMatrix.Avalonia.ViewModels.Base;

namespace StabilityMatrix.Avalonia.ViewModels.HuggingFacePage;

public partial class CategoryViewModel : ViewModelBase
{
    [ObservableProperty]
    private IObservableCollection<HuggingfaceItemViewModel> items =
        new ObservableCollectionExtended<HuggingfaceItemViewModel>();

    public SourceCache<HuggingfaceItem, string> ItemsCache { get; } =
        new(i => i.RepositoryPath + i.ModelName);

    [ObservableProperty]
    private string? title;

    [ObservableProperty]
    private bool isChecked;

    [ObservableProperty]
    private int numSelected;

    public CategoryViewModel(IEnumerable<HuggingfaceItem> items)
    {
        ItemsCache
            .Connect()
            .DeferUntilLoaded()
            .Transform(i => new HuggingfaceItemViewModel { Item = i })
            .Bind(Items)
            .WhenPropertyChanged(p => p.IsSelected)
            .Subscribe(_ => NumSelected = Items.Count(i => i.IsSelected));

        ItemsCache.EditDiff(items, (a, b) => a.RepositoryPath == b.RepositoryPath);
    }

    partial void OnIsCheckedChanged(bool value)
    {
        if (Items is null)
            return;

        foreach (var item in Items)
        {
            item.IsSelected = value;
        }
    }
}
