using StabilityMatrix.Models;

namespace StabilityMatrix.Helper;

public interface ISettingsManager
{
    Settings Settings { get; }
    void SetTheme(string theme);
}