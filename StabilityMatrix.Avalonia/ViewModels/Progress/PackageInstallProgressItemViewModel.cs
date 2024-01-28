using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Threading;
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
    private readonly IReadOnlyList<IPackageStep>? packageSteps;
    private BetterContentDialog? dialog;

    public PackageInstallProgressItemViewModel(
        IPackageModificationRunner packageModificationRunner,
        IReadOnlyList<IPackageStep>? packageSteps = null,
        Action? onCompleted = null
    )
    {
        this.packageModificationRunner = packageModificationRunner;
        this.packageSteps = packageSteps;

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

        if (packageSteps is not { Count: > 0 })
            return;

        var task = packageModificationRunner.ExecuteSteps(packageSteps);

        if (onCompleted is not null)
        {
            task.ContinueWith(_ => onCompleted.Invoke()).SafeFireAndForget();
        }
        else
        {
            task.SafeFireAndForget();
        }
    }

    private void PackageModificationRunnerOnProgressChanged(object? sender, ProgressReport e)
    {
        Progress.Value = e.Percentage;
        Progress.Description = e.Message;
        Progress.IsIndeterminate = e.IsIndeterminate;
        Progress.Text = packageModificationRunner.CurrentStep?.ProgressTitle;
        Name = packageModificationRunner.CurrentStep?.ProgressTitle;
        Failed = packageModificationRunner.Failed;

        if (string.IsNullOrWhiteSpace(e.Message) || e.Message.Contains("Downloading..."))
            return;

        Progress.Console.PostLine(e.Message);
        EventManager.Instance.OnScrollToBottomRequested();

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
        Progress.CloseWhenFinished = true;
        dialog = new BetterContentDialog
        {
            MaxDialogWidth = 900,
            MinDialogWidth = 900,
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            IsFooterVisible = false,
            Content = new PackageModificationDialog { DataContext = Progress }
        };
        EventManager.Instance.OnToggleProgressFlyout();
        await dialog.ShowAsync();
    }
}
