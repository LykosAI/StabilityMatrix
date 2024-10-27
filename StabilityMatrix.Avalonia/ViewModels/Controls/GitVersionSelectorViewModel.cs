using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Core.Git;

namespace StabilityMatrix.Avalonia.ViewModels.Controls;

public partial class GitVersionSelectorViewModel : ObservableObject
{
    [ObservableProperty]
    private IGitVersionProvider? gitVersionProvider;

    public BetterContentDialog GetDialog()
    {
        Dispatcher.UIThread.VerifyAccess();

        var selector = new GitVersionSelector
        {
            DataContext = this,
            Height = 400,
            Width = 600,
            [!GitVersionSelector.GitVersionProviderProperty] = new Binding(nameof(GitVersionProvider))
        };

        var dialog = new BetterContentDialog
        {
            Content = selector,
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            PrimaryButtonText = Resources.Action_Save,
            CloseButtonText = Resources.Action_Cancel,
            DefaultButton = ContentDialogButton.Primary,
            MinDialogWidth = 400
        };

        return dialog;
    }
}
