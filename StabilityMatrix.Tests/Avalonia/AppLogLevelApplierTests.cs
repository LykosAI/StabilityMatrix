using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using StabilityMatrix.Avalonia.Logging;
using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class AppLogLevelApplierTests
{
    private static (LoggingConfiguration Config, MemoryTarget Memory) CreateConfig()
    {
        var memory = new MemoryTarget("memory") { Layout = "${level}|${logger}|${message}" };
        var factory = new LogFactory();
        var config = new LoggingConfiguration(factory);

        // Mirrors App.ConfigureLogging: a filter for a specific logger, then catch-all file and console rules
        config.AddRule(
            new LoggingRule("System.*", LogLevel.Trace, LogLevel.Info, new NullTarget()) { Final = true }
        );
        config.AddRule(
            LogLevel.Debug,
            LogLevel.Fatal,
            new AsyncTargetWrapper(
                new FileTarget("logfile") { FileName = Path.Combine(Path.GetTempPath(), "unused.log") }
            )
        );
        config.AddRule(
            LogLevel.Trace,
            LogLevel.Fatal,
            new AsyncTargetWrapper(new RichConsoleTarget("console"))
        );
        config.AddRule(LogLevel.Trace, LogLevel.Fatal, memory);

        return (config, memory);
    }

    [DataTestMethod]
    [DataRow(AppLogLevel.Trace)]
    [DataRow(AppLogLevel.Debug)]
    [DataRow(AppLogLevel.Info)]
    [DataRow(AppLogLevel.Warn)]
    [DataRow(AppLogLevel.Error)]
    public void Apply_SetsFileAndConsoleRules(AppLogLevel level)
    {
        var (config, _) = CreateConfig();

        var updated = AppLogLevelApplier.Apply(config, level);

        Assert.AreEqual(2, updated);
        var expected = level.ToNLogLevel();
        foreach (var rule in config.LoggingRules.Skip(1).Take(2))
        {
            foreach (var l in LogLevel.AllLoggingLevels)
            {
                Assert.AreEqual(l >= expected, rule.IsLoggingEnabledForLevel(l), $"{l} with setting {level}");
            }
        }
    }

    [TestMethod]
    public void Apply_LeavesOtherRulesUnchanged()
    {
        var (config, _) = CreateConfig();

        AppLogLevelApplier.Apply(config, AppLogLevel.Error);

        var systemRule = config.LoggingRules[0];
        Assert.IsTrue(systemRule.IsLoggingEnabledForLevel(LogLevel.Trace));
        Assert.IsTrue(systemRule.IsLoggingEnabledForLevel(LogLevel.Info));

        var memoryRule = config.LoggingRules[3];
        Assert.IsTrue(memoryRule.IsLoggingEnabledForLevel(LogLevel.Trace));
    }

    [TestMethod]
    public void Apply_NullConfiguration_ReturnsZero()
    {
        Assert.AreEqual(0, AppLogLevelApplier.Apply(null, AppLogLevel.Info));
    }
}
