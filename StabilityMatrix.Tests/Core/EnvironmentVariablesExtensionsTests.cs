using System.Collections.Immutable;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class EnvironmentVariablesExtensionsTests
{
    private static SettingsManager CreateSettingsManager() => new(NullLogger<SettingsManager>.Instance);

    /// <summary>
    /// A substitute <see cref="IPyVenvRunner"/> whose <see cref="IPyVenvRunner.UpdateEnvironmentVariables"/>
    /// actually mutates <see cref="IPyVenvRunner.EnvironmentVariables"/>, so tests can exercise real
    /// read-modify-write semantics instead of mocking every call site.
    /// </summary>
    private static IPyVenvRunner CreateFakeVenvRunner(ImmutableDictionary<string, string> initial)
    {
        var venvRunner = Substitute.For<IPyVenvRunner>();
        venvRunner.EnvironmentVariables = initial;
        venvRunner
            .When(x =>
                x.UpdateEnvironmentVariables(
                    Arg.Any<Func<ImmutableDictionary<string, string>, ImmutableDictionary<string, string>>>()
                )
            )
            .Do(callInfo =>
            {
                var update = callInfo.Arg<
                    Func<ImmutableDictionary<string, string>, ImmutableDictionary<string, string>>
                >();
                venvRunner.EnvironmentVariables = update(venvRunner.EnvironmentVariables);
            });
        return venvRunner;
    }

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

    [TestMethod]
    public async Task RunWithTemporaryEnvironmentVariableAsync_RestoresPreviousValue_WhenOneExisted()
    {
        var venvRunner = CreateFakeVenvRunner(
            ImmutableDictionary<string, string>.Empty.Add("UV_SKIP_WHEEL_FILENAME_CHECK", "0")
        );

        string? valueDuringAction = null;
        await venvRunner.RunWithTemporaryEnvironmentVariableAsync(
            "UV_SKIP_WHEEL_FILENAME_CHECK",
            "1",
            () =>
            {
                valueDuringAction = venvRunner.EnvironmentVariables["UV_SKIP_WHEEL_FILENAME_CHECK"];
                return Task.CompletedTask;
            }
        );

        Assert.AreEqual("1", valueDuringAction);
        Assert.AreEqual("0", venvRunner.EnvironmentVariables["UV_SKIP_WHEEL_FILENAME_CHECK"]);
    }

    [TestMethod]
    public async Task RunWithTemporaryEnvironmentVariableAsync_RemovesKey_WhenNoneExistedBefore()
    {
        var venvRunner = CreateFakeVenvRunner(ImmutableDictionary<string, string>.Empty);

        await venvRunner.RunWithTemporaryEnvironmentVariableAsync(
            "UV_SKIP_WHEEL_FILENAME_CHECK",
            "1",
            () => Task.CompletedTask
        );

        Assert.IsFalse(venvRunner.EnvironmentVariables.ContainsKey("UV_SKIP_WHEEL_FILENAME_CHECK"));
    }

    [TestMethod]
    public async Task RunWithTemporaryEnvironmentVariableAsync_RestoresPreviousValue_EvenWhenActionThrows()
    {
        var venvRunner = CreateFakeVenvRunner(
            ImmutableDictionary<string, string>.Empty.Add("UV_SKIP_WHEEL_FILENAME_CHECK", "0")
        );

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            venvRunner.RunWithTemporaryEnvironmentVariableAsync(
                "UV_SKIP_WHEEL_FILENAME_CHECK",
                "1",
                () => throw new InvalidOperationException()
            )
        );

        Assert.AreEqual("0", venvRunner.EnvironmentVariables["UV_SKIP_WHEEL_FILENAME_CHECK"]);
    }
}
