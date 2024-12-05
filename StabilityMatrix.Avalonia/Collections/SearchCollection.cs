using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using DynamicData;
using DynamicData.Binding;
using JetBrains.Annotations;

namespace StabilityMatrix.Avalonia.Collections;

[PublicAPI]
public class SearchCollection<TObject, TKey, TQuery> : AbstractNotifyPropertyChanged, IDisposable
    where TObject : notnull
    where TKey : notnull
{
    private readonly IDisposable cleanUp;

    private Func<TQuery?, Func<TObject, bool>>? PredicateSelector { get; }
    private Func<TQuery?, Func<TObject, (bool, int)>>? ScorerSelector { get; }
    private Func<TObject, (bool, int)>? Scorer { get; set; }

    private TQuery? _query;
    public TQuery? Query
    {
        get => _query;
        set => SetAndRaise(ref _query, value);
    }

    private SortExpressionComparer<TObject> _sortComparer = [];
    public SortExpressionComparer<TObject> SortComparer
    {
        get => _sortComparer;
        set => SetAndRaise(ref _sortComparer, value);
    }

    /// <summary>
    /// Converts <see cref="SortComparer"/> to <see cref="SortExpressionComparer{SearchItem}"/>.
    /// </summary>
    private SortExpressionComparer<SearchItem> SearchItemSortComparer =>
        [
            ..SortComparer
        .Select(sortExpression => new SortExpression<SearchItem>(
            item => sortExpression.Expression(item.Item),
            sortExpression.Direction
        )).Prepend(new SortExpression<SearchItem>(item => item.Score, SortDirection.Descending))
        ];

    public IObservableCollection<TObject> Items { get; } = new ObservableCollectionExtended<TObject>();

    public IObservableCollection<TObject> FilteredItems { get; } =
        new ObservableCollectionExtended<TObject>();

    public SearchCollection(
        IObservable<IChangeSet<TObject, TKey>> source,
        Func<TQuery?, Func<TObject, bool>> predicateSelector,
        SortExpressionComparer<TObject>? sortComparer = null
    )
    {
        PredicateSelector = predicateSelector;

        if (sortComparer is not null)
        {
            SortComparer = sortComparer;
        }

        // Observable which creates a new predicate whenever Query property changes
        var dynamicPredicate = this.WhenValueChanged(@this => @this.Query).Select(predicateSelector);

        cleanUp = source
            .Bind(Items)
            .Filter(dynamicPredicate)
            .Sort(SortComparer)
            .Bind(FilteredItems)
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe();
    }

    public SearchCollection(
        IObservable<IChangeSet<TObject, TKey>> source,
        Func<TQuery?, Func<TObject, (bool, int)>> scorerSelector,
        SortExpressionComparer<TObject>? sortComparer = null
    )
    {
        ScorerSelector = scorerSelector;

        if (sortComparer is not null)
        {
            SortComparer = sortComparer;
        }

        // Monitor Query property for changes
        var queryChanged = this.WhenValueChanged(@this => @this.Query).Select(_ => Unit.Default);

        cleanUp = new CompositeDisposable(
            // Update Scorer property whenever Query property changes
            queryChanged.Subscribe(_ => Scorer = scorerSelector(Query)),
            // Transform source items into SearchItems
            source
                .Transform(
                    obj =>
                    {
                        var (isMatch, score) = Scorer?.Invoke(obj) ?? (true, 0);

                        return new SearchItem
                        {
                            Item = obj,
                            IsMatch = isMatch,
                            Score = score
                        };
                    },
                    forceTransform: queryChanged
                )
                .Filter(item => item.IsMatch)
                .Sort(SearchItemSortComparer, SortOptimisations.ComparesImmutableValuesOnly)
                .Transform(searchItem => searchItem.Item)
                .Bind(FilteredItems)
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe()
        );
    }

    /// <summary>
    /// Clears <see cref="Query"/> property by setting it to default value.
    /// </summary>
    public void ClearQuery()
    {
        Query = default;
    }

    public void Dispose()
    {
        cleanUp.Dispose();
        GC.SuppressFinalize(this);
    }

    private readonly record struct SearchItem
    {
        public TObject Item { get; init; }
        public int Score { get; init; }
        public bool IsMatch { get; init; }
    }
}
