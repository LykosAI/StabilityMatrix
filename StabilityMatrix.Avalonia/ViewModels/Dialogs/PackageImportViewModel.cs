using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using NLog;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(PackageImportDialog))]
public partial class PackageImportViewModel : ContentDialogViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    private readonly IPackageFactory packageFactory;
    private readonly ISettingsManager settingsManager;
    
    [ObservableProperty] private DirectoryPath? packagePath;
    [ObservableProperty] private BasePackage? selectedBasePackage;
    
    public IReadOnlyList<BasePackage> AvailablePackages 
        => packageFactory.GetAllAvailablePackages().ToImmutableArray();
    
    [ObservableProperty] private PackageVersion? selectedVersion;
    
    [ObservableProperty] private ObservableCollection<GitCommit>? availableCommits;
    [ObservableProperty] private ObservableCollection<PackageVersion>? availableVersions;
    
    [ObservableProperty] private GitCommit? selectedCommit;
    
    // Version types (release or commit)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReleaseLabelText),
        nameof(IsReleaseMode), nameof(SelectedVersion))]
    private PackageVersionType selectedVersionType = PackageVersionType.Commit;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReleaseModeAvailable))]
    private PackageVersionType availableVersionTypes = 
        PackageVersionType.GithubRelease | PackageVersionType.Commit;
    public string ReleaseLabelText => IsReleaseMode ? "Version" : "Branch";
    public bool IsReleaseMode
    {
        get => SelectedVersionType == PackageVersionType.GithubRelease;
        set => SelectedVersionType = value ? PackageVersionType.GithubRelease : PackageVersionType.Commit;
    }

    public bool IsReleaseModeAvailable => AvailableVersionTypes.HasFlag(PackageVersionType.GithubRelease);
    
    public PackageImportViewModel(
        IPackageFactory packageFactory,
        ISettingsManager settingsManager)
    {
        this.packageFactory = packageFactory;
        this.settingsManager = settingsManager;
    }

    public override async Task OnLoadedAsync()
    {
        SelectedBasePackage ??= AvailablePackages[0];
        
        if (Design.IsDesignMode) return;
        // Populate available versions
        try
        {
            if (IsReleaseMode)
            {
                var versions = (await SelectedBasePackage.GetAllVersions()).ToList();
                AvailableVersions = new ObservableCollection<PackageVersion>(versions);
                if (!AvailableVersions.Any()) return;

                SelectedVersion = AvailableVersions[0];
            }
            else
            {
                var branches = (await SelectedBasePackage.GetAllBranches()).ToList();
                AvailableVersions = new ObservableCollection<PackageVersion>(branches.Select(b =>
                    new PackageVersion
                    {
                        TagName = b.Name,
                        ReleaseNotesMarkdown = b.Commit.Label
                    }));
                UpdateSelectedVersionToLatestMain();
            }
        }
        catch (Exception e)
        {
            Logger.Warn("Error getting versions: {Exception}", e.ToString());
        }
    }
    
    private static string GetDisplayVersion(string version, string? branch)
    {
        return branch == null ? version : $"{branch}@{version[..7]}";
    }
    
    // When available version types change, reset selected version type if not compatible
    partial void OnAvailableVersionTypesChanged(PackageVersionType value)
    {
        if (!value.HasFlag(SelectedVersionType))
        {
            SelectedVersionType = value;
        }
    }
    
    // When changing branch / release modes, refresh
    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSelectedVersionTypeChanged(PackageVersionType value) 
        => OnSelectedBasePackageChanged(SelectedBasePackage);

    partial void OnSelectedBasePackageChanged(BasePackage? value)
    {
        if (value is null || SelectedBasePackage is null)
        {
            AvailableVersions?.Clear();
            AvailableCommits?.Clear();
            return;
        }
        
        AvailableVersions?.Clear();
        AvailableCommits?.Clear();

        AvailableVersionTypes = SelectedBasePackage.ShouldIgnoreReleases
            ? PackageVersionType.Commit
            : PackageVersionType.GithubRelease | PackageVersionType.Commit;
        
        if (Design.IsDesignMode) return;
        
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            Logger.Debug($"Release mode: {IsReleaseMode}");
            var versions = (await value.GetAllVersions(IsReleaseMode)).ToList();
            
            if (!versions.Any()) return;

            AvailableVersions = new ObservableCollection<PackageVersion>(versions);
            Logger.Debug($"Available versions: {string.Join(", ", AvailableVersions)}");
            SelectedVersion = AvailableVersions[0];
            
            if (!IsReleaseMode)
            {
                var commits = (await value.GetAllCommits(SelectedVersion.TagName))?.ToList();
                if (commits is null || commits.Count == 0) return;
                
                AvailableCommits = new ObservableCollection<GitCommit>(commits);
                SelectedCommit = AvailableCommits[0];
                UpdateSelectedVersionToLatestMain();
            }
        }).SafeFireAndForget();
    }
    
    private void UpdateSelectedVersionToLatestMain()
    {
        if (AvailableVersions is null)
        {
            SelectedVersion = null;
        }
        else
        {
            // First try to find master
            var version = AvailableVersions.FirstOrDefault(x => x.TagName == "master");
            // If not found, try main
            version ??= AvailableVersions.FirstOrDefault(x => x.TagName == "main");
        
            // If still not found, just use the first one
            version ??= AvailableVersions[0];
        
            SelectedVersion = version;
        }
    }
    
    public async Task AddPackageWithCurrentInputs()
    {
        if (SelectedBasePackage is null || PackagePath is null)
            return;

        string version;
        if (IsReleaseMode)
        {
            version = SelectedVersion?.TagName ?? 
                      throw new NullReferenceException("Selected version is null");
        }
        else
        {
            version = SelectedCommit?.Sha ?? 
                      throw new NullReferenceException("Selected commit is null");
        }
        
        var branch = SelectedVersionType == PackageVersionType.GithubRelease ? 
            null : SelectedVersion!.TagName;
        
        var package = new InstalledPackage
        {
            Id = Guid.NewGuid(),
            DisplayName = PackagePath.Name,
            PackageName = SelectedBasePackage.Name,
            LibraryPath = $"Packages{Path.DirectorySeparatorChar}{PackagePath.Name}",
            PackageVersion = version,
            DisplayVersion = GetDisplayVersion(version, branch),
            InstalledBranch = branch,
            LaunchCommand = SelectedBasePackage.LaunchCommand,
            LastUpdateCheck = DateTimeOffset.Now,
        };
        
        // Recreate venv if it's a BaseGitPackage
        if (SelectedBasePackage is BaseGitPackage gitPackage)
        {
            await gitPackage.SetupVenv(PackagePath, forceRecreate: true);
        }
        
        // Reconfigure shared links
        await SelectedBasePackage.UpdateModelFolders(PackagePath);
        
        settingsManager.Transaction(s => s.InstalledPackages.Add(package));
    }
}
