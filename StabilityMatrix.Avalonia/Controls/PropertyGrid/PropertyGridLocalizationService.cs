using System;
using PropertyModels.ComponentModel;
using PropertyModels.Localilzation;
using StabilityMatrix.Avalonia.Languages;

namespace StabilityMatrix.Avalonia.Controls;

/// <summary>
/// Implements <see cref="ILocalizationService"/> using static <see cref="Cultures"/>.
/// </summary>
internal class PropertyGridLocalizationService : MiniReactiveObject, ILocalizationService
{
    /// <inheritdoc />
    public ICultureData CultureData { get; } = new PropertyGridCultureData();

    /// <inheritdoc />
    public string this[string key] => CultureData[key];

    /// <inheritdoc />
    public event EventHandler? OnCultureChanged;

    /// <inheritdoc />
    public ILocalizationService[] GetExtraServices() => Array.Empty<ILocalizationService>();

    /// <inheritdoc />
    public void AddExtraService(ILocalizationService service) { }

    /// <inheritdoc />
    public void RemoveExtraService(ILocalizationService service) { }

    /// <inheritdoc />
    public ICultureData[] GetCultures() => new[] { CultureData };

    /// <inheritdoc />
    public void SelectCulture(string cultureName) { }
}
