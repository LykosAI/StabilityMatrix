using DynamicData.Binding;
using JetBrains.Annotations;

namespace StabilityMatrix.Core.Models;

[PublicAPI]
public class SelectableItem<T>(T item) : AbstractNotifyPropertyChanged, IEquatable<SelectableItem<T>>
{
    public T Item { get; } = item;

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetAndRaise(ref _isSelected, value);
    }

    /// <inheritdoc />
    public bool Equals(SelectableItem<T>? other)
    {
        if (ReferenceEquals(null, other))
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return EqualityComparer<T>.Default.Equals(Item, other.Item);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (obj.GetType() != GetType())
            return false;
        return Equals((SelectableItem<T>)obj);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(GetType().GetHashCode(), Item?.GetHashCode());
    }
}
