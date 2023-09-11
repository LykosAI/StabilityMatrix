using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Avalonia.ViewModels.Base;

public partial class ConsoleProgressViewModel : ProgressViewModel
{
    public ConsoleViewModel Console { get; } = new();

    [ObservableProperty]
    private bool closeWhenFinished;
}
