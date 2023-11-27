using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Avalonia.Helpers;

public static class IOCommands
{
    public static RelayCommand<string?> OpenUrlCommand { get; } =
        new(
            url =>
            {
                if (string.IsNullOrWhiteSpace(url))
                    return;

                ProcessRunner.OpenUrl(url);
            },
            url => !string.IsNullOrWhiteSpace(url)
        );
}
