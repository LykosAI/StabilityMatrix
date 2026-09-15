using System.Collections.Immutable;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Extensions;

public static class EnvironmentVariablesExtensions
{
    /// <summary>
    /// Sets a package-computed default/workaround environment variable, unless the user has
    /// explicitly configured that key themselves under Settings > Environment Variables.
    /// </summary>
    /// <remarks>
    /// Use this instead of <see cref="ImmutableDictionary{TKey,TValue}.SetItem"/> for any env var
    /// that exists to work around an issue (e.g. <c>SETUPTOOLS_USE_DISTUTILS</c>) rather than to
    /// record a fact about the install (e.g. an install path) - a user who has set the same key
    /// to fix something on their own machine should always win over our guess.
    /// </remarks>
    public static ImmutableDictionary<string, string> SetPackageDefault(
        this ImmutableDictionary<string, string> env,
        ISettingsManager settingsManager,
        string key,
        string value
    ) => settingsManager.Settings.IsEnvironmentVariableUserOverridden(key) ? env : env.SetItem(key, value);
}
