using Avalonia.Input;

namespace StabilityMatrix.Avalonia.Extensions;

public static class DataObjectExtensions
{
    /// <summary>
    /// Get Context from IDataObject, set by Xaml Behaviors
    /// </summary>
    public static T? GetContext<T>(this IDataObject dataObject)
    {
        if (dataObject.Get("Context") is T context)
        {
            return context;
        }

        return default;
    }
}
