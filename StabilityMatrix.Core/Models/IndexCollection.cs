using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models;

public class IndexCollection<TObject, TKey>
    where TKey : notnull
{
    private readonly IImageIndexService imageIndexService;

    public string? RelativePath { get; set; }

    public SourceCache<TObject, TKey> ItemsSource { get; }

    /// <summary>
    /// Observable Collection of indexed items
    /// </summary>
    public IObservableCollection<TObject> Items { get; } = new ObservableCollectionExtended<TObject>();

    public IndexCollection(
        IImageIndexService imageIndexService,
        Func<TObject, TKey> keySelector,
        Func<IObservable<IChangeSet<TObject, TKey>>, IObservable<IChangeSet<TObject, TKey>>>? transform = null
    )
    {
        this.imageIndexService = imageIndexService;

        ItemsSource = new SourceCache<TObject, TKey>(keySelector);

        var source = ItemsSource.Connect().DeferUntilLoaded();

        if (transform is not null)
        {
            source = transform(source);
        }

        source.Bind(Items).ObserveOn(SynchronizationContext.Current).Subscribe();
    }

    public void Add(TObject item)
    {
        ItemsSource.AddOrUpdate(item);
    }

    public void Remove(TObject item)
    {
        ItemsSource.Remove(item);
    }

    public void RemoveKey(TKey key)
    {
        ItemsSource.RemoveKey(key);
    }
}
