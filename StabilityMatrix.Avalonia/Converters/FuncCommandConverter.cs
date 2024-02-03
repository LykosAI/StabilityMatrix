using System;
using System.Globalization;
using System.Windows.Input;
using Avalonia.Data.Converters;
using PropertyModels.ComponentModel;

namespace StabilityMatrix.Avalonia.Converters;

/// <summary>
/// Converts an object's named <see cref="Func{TResult}"/> to a <see cref="ICommand"/>.
/// </summary>
public class FuncCommandConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
        {
            return null;
        }

        // Parameter is the name of the Func<T> to convert.
        if (parameter is not string funcName)
        {
            // ReSharper disable once LocalizableElement
            throw new ArgumentException("Parameter must be a string.", nameof(parameter));
        }

        // Find the Func<T> on the object.
        if (value.GetType().GetMethod(funcName) is not { } methodInfo)
        {
            // ReSharper disable once LocalizableElement
            throw new ArgumentException(
                $"Method {funcName} not found on {value.GetType().Name}.",
                nameof(parameter)
            );
        }

        // Create a delegate from the method info.
        var func = (Action)methodInfo.CreateDelegate(typeof(Action), value);

        // Create ICommand
        var command = ReactiveCommand.Create(func);

        return command;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
