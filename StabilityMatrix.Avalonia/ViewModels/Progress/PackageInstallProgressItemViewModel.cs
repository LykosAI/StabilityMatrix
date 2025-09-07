using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Avalonia.ViewModels.Progress;

public class PackageInstallProgressItemViewModel : ProgressItemViewModelBase
{
    private readonly IPackageModificationRunner packageModificationRunner;
    private BetterContentDialog? dialog;

    public PackageInstallProgressItemViewModel(IPackageModificationRunner packageModificationRunner)
    {
        this.packageModificationRunner = packageModificationRunner;

        Id = packageModificationRunner.Id;
        Name = packageModificationRunner.CurrentStep?.ProgressTitle;
        Progress.Value = packageModificationRunner.CurrentProgress.Percentage;
        Progress.Text = packageModificationRunner.ConsoleOutput.LastOrDefault();
        Progress.IsIndeterminate = packageModificationRunner.CurrentProgress.IsIndeterminate;
        Progress.HideCloseButton = packageModificationRunner.HideCloseButton;

        if (Design.IsDesignMode)
            return;

        Progress.Console.StartUpdates();
        Progress.Console.Post(string.Join(Environment.NewLine, packageModificationRunner.ConsoleOutput));

        packageModificationRunner.ProgressChanged += PackageModificationRunnerOnProgressChanged;
    }

    private void PackageModificationRunnerOnProgressChanged(object? sender, ProgressReport e)
    {
        Progress.Value = e.Percentage;
        Progress.Description = e.ProcessOutput?.Text ?? e.Message;
        Progress.IsIndeterminate = e.IsIndeterminate;
        Progress.Text = packageModificationRunner.CurrentStep?.ProgressTitle;
        Name = packageModificationRunner.CurrentStep?.ProgressTitle;
        Failed = packageModificationRunner.Failed;

        if (e.ProcessOutput == null && string.IsNullOrWhiteSpace(e.Message))
            return;

        if (!string.IsNullOrWhiteSpace(e.Message) && e.Message.Contains("Downloading..."))
            return;

        if (e is { ProcessOutput: not null, PrintToConsole: true })
        {
            Progress.Console.Post(e.ProcessOutput.Value);
        }
        else if (e.PrintToConsole)
        {
            Progress.Console.PostLine(e.Message);
        }

        if (Progress.AutoScrollToBottom)
        {
            EventManager.Instance.OnScrollToBottomRequested();
        }

        if (
            e is { Message: not null, Percentage: >= 100 }
            && e.Message.Contains(
                packageModificationRunner.ModificationCompleteMessage ?? "Package Install Complete"
            )
            && Progress.CloseWhenFinished
        )
        {
            Dispatcher.UIThread.Post(() => dialog?.Hide());
        }

        if (Failed)
        {
            Progress.Text = "Package Modification Failed";
            Name = "Package Modification Failed";
        }
    }

    public async Task ShowProgressDialog()
    {
        Progress.CloseWhenFinished = packageModificationRunner.CloseWhenFinished;
        dialog = new BetterContentDialog
        {
            MaxDialogWidth = 900,
            MinDialogWidth = 900,
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            IsFooterVisible = false,
            Content = new PackageModificationDialog { DataContext = Progress },
        };
        EventManager.Instance.OnToggleProgressFlyout();
        await dialog.ShowAsync();
    }
}
