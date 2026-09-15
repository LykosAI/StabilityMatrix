using System.Collections.Immutable;
using Microsoft.Extensions.Logging.Abstractions;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class EnvironmentVariablesExtensionsTests
{
    private static SettingsManager CreateSettingsManager() => new(NullLogger<SettingsManager>.Instance);

    [TestMethod]
    public void SetPackageDefault_AppliesValue_WhenUserHasNotOverridden()
    {
        var settingsManager = CreateSettingsManager();
        var env = ImmutableDictionary<string, string>.Empty;

        env = env.SetPackageDefault(settingsManager, "SETUPTOOLS_USE_DISTUTILS", "stdlib");

        Assert.AreEqual("stdlib", env["SETUPTOOLS_USE_DISTUTILS"]);
    }

    [TestMethod]
    public void SetPackageDefault_DoesNotOverride_WhenUserHasSetKey_ListFormat()
    {
        var settingsManager = CreateSettingsManager();
        settingsManager.Settings.UserEnvironmentVariablesList =
        [
            new EnvVarKeyPair("SETUPTOOLS_USE_DISTUTILS", "local", isEnabled: true),
        ];
        var env = ImmutableDictionary<string, string>.Empty;

        env = env.SetPackageDefault(settingsManager, "SETUPTOOLS_USE_DISTUTILS", "stdlib");

        // The package default is skipped entirely - the user's own value (already merged into
        // env upstream via Settings.EnvironmentVariables) is left untouched.
        Assert.IsFalse(env.ContainsKey("SETUPTOOLS_USE_DISTUTILS"));
    }

    [TestMethod]
    public void SetPackageDefault_AppliesValue_WhenUserOverrideIsDisabled()
    {
        var settingsManager = CreateSettingsManager();
        settingsManager.Settings.UserEnvironmentVariablesList =
        [
            new EnvVarKeyPair("SETUPTOOLS_USE_DISTUTILS", "local", isEnabled: false),
        ];
        var env = ImmutableDictionary<string, string>.Empty;

        env = env.SetPackageDefault(settingsManager, "SETUPTOOLS_USE_DISTUTILS", "stdlib");

        Assert.AreEqual("stdlib", env["SETUPTOOLS_USE_DISTUTILS"]);
    }

    [TestMethod]
    public void SetPackageDefault_DoesNotOverride_WhenUserHasSetKey_LegacyDictFormat()
    {
        var settingsManager = CreateSettingsManager();
        settingsManager.Settings.UserEnvironmentVariables = new Dictionary<string, string>
        {
            ["SETUPTOOLS_USE_DISTUTILS"] = "local",
        };
        var env = ImmutableDictionary<string, string>.Empty;

        env = env.SetPackageDefault(settingsManager, "SETUPTOOLS_USE_DISTUTILS", "stdlib");

        Assert.IsFalse(env.ContainsKey("SETUPTOOLS_USE_DISTUTILS"));
    }

    [TestMethod]
    public void SetPackageDefault_LaterCallCanStillOverride_WhenKeyNotUserSet()
    {
        // Simulates a derived package (e.g. Reforge) overriding a base package's own default
        // (e.g. A3WebUI's STABLE_DIFFUSION_REPO) - internal chain precedence must keep working
        // when the user hasn't configured the key themselves.
        var settingsManager = CreateSettingsManager();
        var env = ImmutableDictionary<string, string>.Empty;

        env = env.SetPackageDefault(settingsManager, "STABLE_DIFFUSION_REPO", "https://base/default.git");
        env = env.SetPackageDefault(settingsManager, "STABLE_DIFFUSION_REPO", "https://derived/override.git");

        Assert.AreEqual("https://derived/override.git", env["STABLE_DIFFUSION_REPO"]);
    }
}
