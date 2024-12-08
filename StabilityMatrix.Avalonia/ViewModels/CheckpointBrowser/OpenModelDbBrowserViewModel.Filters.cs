using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using StabilityMatrix.Core.Models.Api.OpenModelsDb;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;

public partial class OpenModelDbBrowserViewModel
{
    [ObservableProperty]
    private string? searchQuery;

    private Subject<Unit> SearchQueryReload { get; } = new();

    private IObservable<Func<OpenModelDbModel, bool>> SearchQueryPredicate =>
        SearchQueryReload
            .Select(pv => CreateSearchQueryPredicate(SearchQuery))
            .StartWith(CreateSearchQueryPredicate(null))
            .ObserveOn(SynchronizationContext.Current!)
            .AsObservable();

    private IObservable<SortExpressionComparer<OpenModelDbBrowserCardViewModel>> SortComparer =>
        Observable
            .FromEventPattern<PropertyChangedEventArgs>(this, nameof(PropertyChanged))
            .Where(x => x.EventArgs.PropertyName is nameof(SelectedSortOption))
            .Throttle(TimeSpan.FromMilliseconds(50))
            .Select(_ => GetSortComparer(SelectedSortOption))
            .StartWith(GetSortComparer(SelectedSortOption))
            .ObserveOn(SynchronizationContext.Current!)
            .AsObservable();

    private static Func<OpenModelDbModel, bool> CreateSearchQueryPredicate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return static _ => true;
        }

        return x =>
            x.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) == true
            || x.Tags?.Any(tag => tag.StartsWith(text, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static SortExpressionComparer<OpenModelDbBrowserCardViewModel> GetSortComparer(
        string sortOption
    ) =>
        sortOption switch
        {
            "Latest"
                => SortExpressionComparer<OpenModelDbBrowserCardViewModel>.Descending(x => x.Model?.Date),
            "Largest Scale"
                => SortExpressionComparer<OpenModelDbBrowserCardViewModel>.Descending(x => x.Model?.Scale),
            "Smallest Scale"
                => SortExpressionComparer<OpenModelDbBrowserCardViewModel>.Ascending(x => x.Model?.Scale),
            "Largest Size"
                => SortExpressionComparer<OpenModelDbBrowserCardViewModel>.Descending(
                    x => x.Model?.Size?.FirstOrDefault()
                ),
            "Smallest Size"
                => SortExpressionComparer<OpenModelDbBrowserCardViewModel>.Ascending(
                    x => x.Model?.Size?.FirstOrDefault()
                ),
            _ => SortExpressionComparer<OpenModelDbBrowserCardViewModel>.Descending(x => x.Model?.Date)
        };
}
