using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
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
}
