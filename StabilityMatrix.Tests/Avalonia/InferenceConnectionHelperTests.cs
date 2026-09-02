using System.ComponentModel;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Helper.Analytics;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class InferenceConnectionHelperTests
{
    private static RunningPackageService CreateRunningPackageService() =>
        new(
            Substitute.For<ILogger<RunningPackageService>>(),
            Substitute.For<IPackageFactory>(),
            Substitute.For<INotificationService>(),
            Substitute.For<ISettingsManager>(),
            Substitute.For<IPyRunner>(),
            Substitute.For<IAnalyticsUsageTracker>()
        );

    private static IInferenceClientManager CreateClientManager(Func<bool> isConnected)
    {
        var clientManager = Substitute.For<IInferenceClientManager>();
        clientManager.IsConnected.Returns(_ => isConnected());
        return clientManager;
    }

    /// <summary>
    /// The connection dialog launches the package through ICommand.Execute, which cannot be
    /// awaited, so nothing is registered as running when the dialog closes. Checking the
    /// connection before the launch finishes sees an idle app and gives up, dropping the
    /// generation the user already asked for.
    /// </summary>
    [TestMethod]
    public async Task WaitForLaunchAndConnection_WaitsForLaunchBeforeGivingUp()
    {
        var isConnected = false;
        var clientManager = CreateClientManager(() => isConnected);

        var launchTask = Task.Run(async () =>
        {
            await Task.Delay(200);
            isConnected = true;
        });

        var result = await InferenceConnectionHelper.WaitForLaunchAndConnectionAsync(
            launchTask,
            clientManager,
            CreateRunningPackageService()
        );

        Assert.IsTrue(result, "Expected the helper to wait for the launch before checking the connection");
    }

    [TestMethod]
    public async Task WaitForLaunchAndConnection_ReturnsFalse_WhenLaunchDoesNotStartAnything()
    {
        var clientManager = CreateClientManager(() => false);

        var result = await InferenceConnectionHelper.WaitForLaunchAndConnectionAsync(
            Task.CompletedTask,
            clientManager,
            CreateRunningPackageService()
        );

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task WaitForLaunchAndConnection_ReturnsFalse_WhenLaunchFaults()
    {
        var clientManager = CreateClientManager(() => false);

        var result = await InferenceConnectionHelper.WaitForLaunchAndConnectionAsync(
            Task.FromException(new InvalidOperationException("launch failed")),
            clientManager,
            CreateRunningPackageService()
        );

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task WaitForConnected_ReturnsTrue_WhenAlreadyConnected()
    {
        var clientManager = CreateClientManager(() => true);

        var result = await InferenceConnectionHelper.WaitForConnectedAsync(
            clientManager,
            CreateRunningPackageService()
        );

        Assert.IsTrue(result);
    }
}
