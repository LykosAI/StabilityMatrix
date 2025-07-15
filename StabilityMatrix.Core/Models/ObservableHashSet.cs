using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace StabilityMatrix.Core.Models;

/// <summary>
/// Represents a observable hash set of values.
/// </summary>
/// <typeparam name="T">The type of elements in the hash set.</typeparam>
/// <summary>
/// An <see cref="ObservableCollection{T}"/> that also implements <see cref="ISet{T}"/> –
/// perfect when you need *unique* items **and** change notifications
/// (e.g. for XAML bindings or ReactiveUI/DynamicData pipelines).
/// </summary>
public class ObservableHashSet<T> : ObservableCollection<T>, ISet<T>
{
    private readonly HashSet<T> set;

    #region ░░░ Constructors ░░░

    public ObservableHashSet()
        : this((IEqualityComparer<T>?)null) { }

    public ObservableHashSet(IEqualityComparer<T>? comparer)
        : base()
    {
        set = new HashSet<T>(comparer ?? EqualityComparer<T>.Default);
    }

    public ObservableHashSet(IEnumerable<T> collection)
        : this(collection, null) { }

    public ObservableHashSet(IEnumerable<T> collection, IEqualityComparer<T>? comparer)
        : this(comparer)
    {
        foreach (var item in collection)
            Add(item); // guarantees uniqueness + raises events
    }

    #endregion

    #region ░░░ Overrides that enforce set semantics ░░░

    /// <summary>
    /// Called by <see cref="Collection{T}.Add"/> (and therefore by LINQ’s
    /// <c>Add</c> extension, by <see cref="ICollection{T}.Add"/>, etc.).
    /// We only insert if the value was *not* already present in the set.
    /// </summary>
    protected override void InsertItem(int index, T item)
    {
        if (set.Add(item)) // true == unique
            base.InsertItem(index, item); // fires events
        // duplicate → ignore silently (same behaviour as HashSet<T>)
    }

    protected override void SetItem(int index, T item)
    {
        var existing = this[index];

        // no-op if same reference/value
        if (EqualityComparer<T>.Default.Equals(existing, item))
            return;

        // attempting to “replace” with a value already in the set → ignore
        if (set.Contains(item))
            return;

        set.Remove(existing);
        set.Add(item);

        base.SetItem(index, item); // fires events
    }

    protected override void RemoveItem(int index)
    {
        set.Remove(this[index]);
        base.RemoveItem(index); // fires events
    }

    protected override void ClearItems()
    {
        set.Clear();
        base.ClearItems(); // fires events
    }

    #endregion

    #region ░░░ ISet<T> explicit implementation ░░░

    // Most operations delegate to the HashSet for the heavy lifting,
    // THEN synchronise the ObservableCollection so that UI bindings
    // get the right notifications.

    bool ISet<T>.Add(T item) => !set.Contains(item) && AddAndReturnTrue(item);

    private bool AddAndReturnTrue(T item)
    {
        base.Add(item);
        return true;
    }

    void ISet<T>.ExceptWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        foreach (var item in other)
            _ = Remove(item); // Remove() already updates both collections
    }

    void ISet<T>.IntersectWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var keep = new HashSet<T>(other, set.Comparer);
        for (var i = Count - 1; i >= 0; i--)
            if (!keep.Contains(this[i]))
                RemoveItem(i);
    }

    void ISet<T>.SymmetricExceptWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var toToggle = new HashSet<T>(other, set.Comparer);

        foreach (var item in toToggle)
            if (!Remove(item))
                Add(item);
    }

    void ISet<T>.UnionWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        foreach (var item in other)
            _ = ((ISet<T>)this).Add(item); // uses our Add logic
    }

    // The pure-query methods just delegate to HashSet<T>:

    bool ISet<T>.IsSubsetOf(IEnumerable<T> other) => set.IsSubsetOf(other);

    bool ISet<T>.IsSupersetOf(IEnumerable<T> other) => set.IsSupersetOf(other);

    bool ISet<T>.IsProperSubsetOf(IEnumerable<T> other) => set.IsProperSubsetOf(other);

    bool ISet<T>.IsProperSupersetOf(IEnumerable<T> other) => set.IsProperSupersetOf(other);

    bool ISet<T>.Overlaps(IEnumerable<T> other) => set.Overlaps(other);

    public bool SetEquals(IEnumerable<T> other) => set.SetEquals(other);

    #endregion

    #region ░░░ Useful helpers ░░░

    public new bool Contains(T item) => set.Contains(item);

    /// <summary>
    /// Returns a copy of the internal <see cref="HashSet{T}"/>.
    /// Handy when you need fast look-ups without exposing mutability.
    /// </summary>
    public HashSet<T> ToHashSet() => new(set, set.Comparer);

    public void AddRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        // 1.  Keep only values that are truly new for this set
        var newItems = new List<T>();
        foreach (var item in items)
            if (set.Add(item)) // true == not yet present
                newItems.Add(item);

        if (newItems.Count == 0)
            return; // nothing to do

        CheckReentrancy(); // ObservableCollection helper

        // 2.  Append to the internal Items list *without* raising events yet
        int startIdx = Items.Count;
        foreach (var item in newItems)
            Items.Add(item);

        // 3.  Fire a single consolidated notification
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));

        //   choose either a single "Add" with the batch…
        OnCollectionChanged(
            new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Add,
                newItems, // the items added
                startIdx
            )
        ); // starting index

        // …or, if you want absolute safety for all consumers,
        // you could raise "Reset" instead:
        // OnCollectionChanged(new NotifyCollectionChangedEventArgs(
        //     NotifyCollectionChangedAction.Reset));
    }

    #endregion
}
