using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class ServiceProviderExtensionsTests
{
    public abstract class TestDisposable : IDisposable
    {
        public abstract void Dispose();
    }

    public abstract class TestAsyncDisposable : IAsyncDisposable
    {
        public abstract ValueTask DisposeAsync();
    }

    [TestMethod]
    public void GetDisposables_ReturnsEmptyList_WhenNoDisposables()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var disposables = serviceProvider.GetDisposables();

        // Assert
        Assert.AreEqual(0, disposables.Count);
    }

    [TestMethod]
    public void GetDisposables_ReturnsEmptyList_WhenNoMaterializedDisposables()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(_ => Substitute.For<TestDisposable>());
        services.AddSingleton(_ => Substitute.For<TestAsyncDisposable>());
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var disposables = serviceProvider.GetDisposables();

        // Assert
        Assert.AreEqual(0, disposables.Count);
    }

    [TestMethod]
    public void GetDisposables_ReturnsDisposables_WhenMaterializedDisposables()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(_ => Substitute.For<TestDisposable>());
        services.AddSingleton(_ => Substitute.For<TestAsyncDisposable>());
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var testDisposable = serviceProvider.GetRequiredService<TestDisposable>();
        var testAsyncDisposable = serviceProvider.GetRequiredService<TestAsyncDisposable>();
        var disposables = serviceProvider.GetDisposables();

        // Assert
        Assert.AreEqual(2, disposables.Count);
        CollectionAssert.Contains(disposables, testDisposable);
        CollectionAssert.Contains(disposables, testAsyncDisposable);
    }

    [TestMethod]
    public void GetDisposables_ReturnsMutableListReference()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(_ => Substitute.For<TestDisposable>());
        var serviceProvider = services.BuildServiceProvider();

        // Act
        // Clearing the list should result in TestDisposable not being disposed by the ServiceProvider
        var testDisposable = serviceProvider.GetRequiredService<TestDisposable>();
        var disposables = serviceProvider.GetDisposables();
        disposables.Clear();
        serviceProvider.Dispose();

        // Assert
        testDisposable.DidNotReceive().Dispose();
    }
}
