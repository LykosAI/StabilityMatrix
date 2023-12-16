using System;
using System.Globalization;
using PropertyModels.Localilzation;
using StabilityMatrix.Avalonia.Languages;

namespace StabilityMatrix.Avalonia.Controls;

internal class PropertyGridCultureData : ICultureData
{
    /// <inheritdoc />
    public bool Reload() => false;

    /// <inheritdoc />
    public CultureInfo Culture => Cultures.Current ?? Cultures.Default;

    /// <inheritdoc />
    public Uri Path => new("");

    /// <inheritdoc />
    public string this[string key]
    {
        get
        {
            if (Resources.ResourceManager.GetString(key) is { } result)
            {
                return result;
            }

            return key;
        }
    }

    /// <inheritdoc />
    public bool IsLoaded => true;
}
