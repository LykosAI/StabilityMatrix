using System.Collections.Immutable;
using StabilityMatrix.Core.Python;
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

    /// <summary>
    /// Forces an environment variable to <paramref name="value"/> for the duration of
    /// <paramref name="action"/>, then restores whatever was there before - the user's own value
    /// if they'd set one, or removes it entirely if it wasn't present.
    /// </summary>
    /// <remarks>
    /// Use for a narrow, one-shot need (e.g. bypassing a wheel filename check for one specific
    /// pinned download) where the override must never outlive the single operation it exists for,
    /// so it can't be left stuck on for the rest of the venv's life by an early return or exception.
    /// </remarks>
    public static async Task RunWithTemporaryEnvironmentVariableAsync(
        this IPyVenvRunner venvRunner,
        string key,
        string value,
        Func<Task> action
    )
    {
        var hadPrevious = venvRunner.EnvironmentVariables.TryGetValue(key, out var previous);

        venvRunner.UpdateEnvironmentVariables(env => env.SetItem(key, value));
        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            venvRunner.UpdateEnvironmentVariables(env =>
                hadPrevious ? env.SetItem(key, previous!) : env.Remove(key)
            );
        }
    }
}
