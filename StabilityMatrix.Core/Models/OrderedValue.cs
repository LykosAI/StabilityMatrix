namespace StabilityMatrix.Core.Models;

public readonly record struct OrderedValue<TValue>(int Order, TValue Value)
    : IComparable<OrderedValue<TValue>>,
        IComparable
{
    private sealed class OrderRelationalComparer : IComparer<OrderedValue<TValue>>
    {
        public int Compare(OrderedValue<TValue> x, OrderedValue<TValue> y)
        {
            return x.Order.CompareTo(y.Order);
        }
    }

    public static IComparer<OrderedValue<TValue>> OrderComparer { get; } = new OrderRelationalComparer();

    public int CompareTo(OrderedValue<TValue> other)
    {
        return Order.CompareTo(other.Order);
    }

    public int CompareTo(object? obj)
    {
        if (ReferenceEquals(null, obj))
            return 1;

        return obj is OrderedValue<TValue> other
            ? CompareTo(other)
            : throw new ArgumentException($"Object must be of type {nameof(OrderedValue<TValue>)}");
    }

    public static bool operator <(OrderedValue<TValue> left, OrderedValue<TValue> right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(OrderedValue<TValue> left, OrderedValue<TValue> right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(OrderedValue<TValue> left, OrderedValue<TValue> right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(OrderedValue<TValue> left, OrderedValue<TValue> right)
    {
        return left.CompareTo(right) >= 0;
    }
}
