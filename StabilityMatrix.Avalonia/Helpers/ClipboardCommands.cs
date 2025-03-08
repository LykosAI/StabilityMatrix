using System;
using CommunityToolkit.Mvvm.Input;

namespace StabilityMatrix.Avalonia.Helpers;

public static class ClipboardCommands
{
    private static readonly Lazy<RelayCommand<string?>> CopyTextCommandLazy =
        new(() => new RelayCommand<string?>(text => App.Clipboard.SetTextAsync(text)));

    public static RelayCommand<string?> CopyTextCommand => CopyTextCommandLazy.Value;
}
