using System;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Controls.Models;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Core.Git;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.ViewModels.Controls;

public partial class GitVersionSelectorViewModel : ObservableObject
{
    [ObservableProperty]
    private IGitVersionProvider? gitVersionProvider;

    [ObservableProperty]
    private string? selectedBranch;

    [ObservableProperty]
    private string? selectedCommit;

    [ObservableProperty]
    private string? selectedTag;

    [ObservableProperty]
    private GitVersionSelectorVersionType selectedVersionType;

    /// <summary>
    /// Gets or sets the selected <see cref="GitVersion"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public GitVersion SelectedGitVersion
    {
        get
        {
            return SelectedVersionType switch
            {
                GitVersionSelectorVersionType.BranchCommit
                    => new GitVersion { Branch = SelectedBranch, CommitSha = SelectedCommit },
                GitVersionSelectorVersionType.Tag => new GitVersion { Tag = SelectedTag },
                _ => throw new InvalidOperationException()
            };
        }
        set
        {
            SelectedVersionType = value switch
            {
                { Tag: not null } => GitVersionSelectorVersionType.Tag,
                { Branch: not null, CommitSha: not null } => GitVersionSelectorVersionType.BranchCommit,
                // Default to branch commit
                _ => GitVersionSelectorVersionType.BranchCommit
            };

            SelectedBranch = value.Branch;
            SelectedCommit = value.CommitSha;
            SelectedTag = value.Tag;
        }
    }

    public BetterContentDialog GetDialog()
    {
        Dispatcher.UIThread.VerifyAccess();

        var selector = new GitVersionSelector
        {
            DataContext = this,
            Height = 400,
            Width = 600,
            [!GitVersionSelector.GitVersionProviderProperty] = new Binding(nameof(GitVersionProvider)),
            [!GitVersionSelector.SelectedVersionTypeProperty] = new Binding(nameof(SelectedVersionType)),
            [!GitVersionSelector.SelectedBranchProperty] = new Binding(nameof(SelectedBranch)),
            [!GitVersionSelector.SelectedCommitProperty] = new Binding(nameof(SelectedCommit)),
            [!GitVersionSelector.SelectedTagProperty] = new Binding(nameof(SelectedTag))
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
