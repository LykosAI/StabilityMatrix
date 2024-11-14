using System;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Binding;
using StabilityMatrix.Core.Models.Api.OpenModelsDb;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;

public partial class OpenModelDbBrowserViewModel
{
    private IObservable<Func<OpenModelDbModel, bool>> SearchQueryPredicate =>
        this.WhenPropertyChanged(vm => vm.SearchQuery)
            .Throttle(TimeSpan.FromMilliseconds(50))
            .Select(pv => CreateSearchQueryPredicate(pv.Value))
            .StartWith(CreateSearchQueryPredicate(null))
            .AsObservable();

    private static Func<OpenModelDbModel, bool> CreateSearchQueryPredicate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return static _ => true;
        }

        return x =>
            x.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) == true
            || x.Tags?.Any(tag => tag.Contains(text, StringComparison.OrdinalIgnoreCase)) == true
            || x.Description?.Contains(text, StringComparison.OrdinalIgnoreCase) == true;
    }
}
