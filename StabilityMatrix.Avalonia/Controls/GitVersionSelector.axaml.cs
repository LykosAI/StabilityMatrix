using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Logging;
using CommunityToolkit.Mvvm.Input;
using Nito.Disposables.Internals;
using StabilityMatrix.Avalonia.Controls.Models;
using StabilityMatrix.Core.Git;

namespace StabilityMatrix.Avalonia.Controls;

[Localizable(false)]
public partial class GitVersionSelector : TemplatedControl
{
    public static readonly StyledProperty<IGitVersionProvider?> GitVersionProviderProperty =
        AvaloniaProperty.Register<GitVersionSelector, IGitVersionProvider?>(nameof(GitVersionProvider));

    public IGitVersionProvider? GitVersionProvider
    {
        get => GetValue(GitVersionProviderProperty);
        set => SetValue(GitVersionProviderProperty, value);
    }

    public static readonly StyledProperty<SelectionMode> BranchSelectionModeProperty =
        AvaloniaProperty.Register<GitVersionSelector, SelectionMode>(nameof(BranchSelectionMode));

    public SelectionMode BranchSelectionMode
    {
        get => GetValue(BranchSelectionModeProperty);
        set => SetValue(BranchSelectionModeProperty, value);
    }

    public static readonly StyledProperty<SelectionMode> CommitSelectionModeProperty =
        AvaloniaProperty.Register<GitVersionSelector, SelectionMode>(nameof(CommitSelectionMode));

    public SelectionMode CommitSelectionMode
    {
        get => GetValue(CommitSelectionModeProperty);
        set => SetValue(CommitSelectionModeProperty, value);
    }

    public static readonly StyledProperty<SelectionMode> TagSelectionModeProperty = AvaloniaProperty.Register<
        GitVersionSelector,
        SelectionMode
    >(nameof(TagSelectionMode));

    public SelectionMode TagSelectionMode
    {
        get => GetValue(TagSelectionModeProperty);
        set => SetValue(TagSelectionModeProperty, value);
    }

    public static readonly StyledProperty<string?> DefaultBranchProperty = AvaloniaProperty.Register<
        GitVersionSelector,
        string?
    >(nameof(DefaultBranch), "main");

    /// <summary>
    /// The default branch to use when no branch is selected. Shows as a placeholder.
    /// </summary>
    public string? DefaultBranch
    {
        get => GetValue(DefaultBranchProperty);
        set => SetValue(DefaultBranchProperty, value);
    }

    public static readonly StyledProperty<string?> DefaultCommitProperty = AvaloniaProperty.Register<
        GitVersionSelector,
        string?
    >(nameof(DefaultCommit), "latest");

    /// <summary>
    /// The default commit to use when no commit is selected. Shows as a placeholder.
    /// </summary>
    public string? DefaultCommit
    {
        get => GetValue(DefaultCommitProperty);
        set => SetValue(DefaultCommitProperty, value);
    }

    public static readonly StyledProperty<IReadOnlyList<string>> BranchSourceProperty =
        AvaloniaProperty.Register<GitVersionSelector, IReadOnlyList<string>>(nameof(BranchSource), []);

    public IReadOnlyList<string> BranchSource
    {
        get => GetValue(BranchSourceProperty);
        set => SetValue(BranchSourceProperty, value);
    }

    public static readonly StyledProperty<IReadOnlyList<string>> CommitSourceProperty =
        AvaloniaProperty.Register<GitVersionSelector, IReadOnlyList<string>>(nameof(CommitSource), []);

    public IReadOnlyList<string> CommitSource
    {
        get => GetValue(CommitSourceProperty);
        set => SetValue(CommitSourceProperty, value);
    }

    public static readonly StyledProperty<IReadOnlyList<string>> TagSourceProperty =
        AvaloniaProperty.Register<GitVersionSelector, IReadOnlyList<string>>(nameof(TagSource), []);

    public IReadOnlyList<string> TagSource
    {
        get => GetValue(TagSourceProperty);
        set => SetValue(TagSourceProperty, value);
    }

    public static readonly StyledProperty<string?> SelectedBranchProperty = AvaloniaProperty.Register<
        GitVersionSelector,
        string?
    >(nameof(SelectedBranch), defaultBindingMode: BindingMode.TwoWay);

    public string? SelectedBranch
    {
        get => GetValue(SelectedBranchProperty);
        set => SetValue(SelectedBranchProperty, value);
    }

    public static readonly StyledProperty<string?> SelectedCommitProperty = AvaloniaProperty.Register<
        GitVersionSelector,
        string?
    >(nameof(SelectedCommit), defaultBindingMode: BindingMode.TwoWay);

    public string? SelectedCommit
    {
        get => GetValue(SelectedCommitProperty);
        set => SetValue(SelectedCommitProperty, value);
    }

    public static readonly StyledProperty<string?> SelectedTagProperty = AvaloniaProperty.Register<
        GitVersionSelector,
        string?
    >(nameof(SelectedTag), defaultBindingMode: BindingMode.TwoWay);

    public string? SelectedTag
    {
        get => GetValue(SelectedTagProperty);
        set => SetValue(SelectedTagProperty, value);
    }

    public static readonly StyledProperty<GitVersionSelectorVersionType> SelectedVersionTypeProperty =
        AvaloniaProperty.Register<GitVersionSelector, GitVersionSelectorVersionType>(
            nameof(SelectedVersionType),
            defaultBindingMode: BindingMode.TwoWay
        );

    public GitVersionSelectorVersionType SelectedVersionType
    {
        get => GetValue(SelectedVersionTypeProperty);
        set => SetValue(SelectedVersionTypeProperty, value);
    }

    public static readonly DirectProperty<
        GitVersionSelector,
        IAsyncRelayCommand
    > PopulateBranchesCommandProperty = AvaloniaProperty.RegisterDirect<
        GitVersionSelector,
        IAsyncRelayCommand
    >(nameof(PopulateBranchesCommand), o => o.PopulateBranchesCommand);

    public static readonly DirectProperty<
        GitVersionSelector,
        IAsyncRelayCommand
    > PopulateCommitsForCurrentBranchCommandProperty = AvaloniaProperty.RegisterDirect<
        GitVersionSelector,
        IAsyncRelayCommand
    >(nameof(PopulateCommitsForCurrentBranchCommand), o => o.PopulateCommitsForCurrentBranchCommand);

    public static readonly DirectProperty<
        GitVersionSelector,
        IAsyncRelayCommand
    > PopulateTagsCommandProperty = AvaloniaProperty.RegisterDirect<GitVersionSelector, IAsyncRelayCommand>(
        nameof(PopulateTagsCommand),
        o => o.PopulateTagsCommand
    );

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (GitVersionProvider is not null)
        {
            PopulateBranchesCommand.Execute(null);

            if (SelectedBranch is not null)
            {
                PopulateCommitsForCurrentBranchCommand.Execute(null);
            }

            PopulateTagsCommand.Execute(null);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // On branch change, fetch commits
        if (change.Property == SelectedBranchProperty)
        {
            PopulateCommitsForCurrentBranchCommand.Execute(null);
        }
    }

    [RelayCommand]
    public async Task PopulateBranches()
    {
        if (GitVersionProvider is null)
            return;

        var branches = await GitVersionProvider.FetchBranchesAsync();

        BranchSource = branches.Select(v => v.Branch).WhereNotNull().ToImmutableList();
    }

    [RelayCommand]
    public async Task PopulateCommitsForCurrentBranch()
    {
        if (string.IsNullOrEmpty(SelectedBranch))
        {
            CommitSource = [];
            return;
        }

        if (GitVersionProvider is null)
            return;

        try
        {
            var commits = await GitVersionProvider.FetchCommitsAsync(SelectedBranch);

            CommitSource = commits.Select(v => v.CommitSha).WhereNotNull().ToImmutableList();
        }
        catch (Exception e)
        {
            Logger
                .TryGet(LogEventLevel.Error, nameof(GitVersionSelector))
                ?.Log(this, "Failed to fetch commits for branch {Branch}: {Exception}", SelectedBranch, e);
        }
    }

    [RelayCommand]
    public async Task PopulateTags()
    {
        if (GitVersionProvider is null)
            return;

        var tags = await GitVersionProvider.FetchTagsAsync();

        TagSource = tags.Select(v => v.Tag).WhereNotNull().ToImmutableList();
    }

    public enum SelectionMode
    {
        Disabled,
        Required,
        Optional
    }
}
