using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using FluentAvalonia.UI.Windowing;

namespace StabilityMatrix.Avalonia.Controls;

internal class ApplicationSplashScreen : IApplicationSplashScreen
{
    public string? AppName { get; init; }

    public IImage? AppIcon { get; init; }

    public object? SplashScreenContent { get; init; }

    public int MinimumShowTime { get; init; }

    public Func<CancellationToken, Task>? InitApp { get; init; }

    public Task RunTasks(CancellationToken cancellationToken)
    {
        return InitApp?.Invoke(cancellationToken) ?? Task.CompletedTask;
    }
}
