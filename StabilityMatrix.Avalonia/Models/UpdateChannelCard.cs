using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Semver;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Update;

namespace StabilityMatrix.Avalonia.Models;

public partial class UpdateChannelCard : ObservableObject
{
    public UpdateChannel UpdateChannel { get; init; }

    public string DisplayName => UpdateChannel.GetStringValue();

    public string? Description { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LatestVersionString))]
    [NotifyPropertyChangedFor(nameof(IsLatestVersionUpdateable))]
    private SemVersion? latestVersion;

    public string? LatestVersionString => LatestVersion is null ? null : $"Latest: v{LatestVersion}";

    [ObservableProperty]
    private bool isSelectable = true;

    /// <summary>
    /// Whether the <see cref="LatestVersion"/> is available for update.
    /// </summary>
    public bool IsLatestVersionUpdateable
    {
        get
        {
            if (LatestVersion is null)
            {
                return false;
            }

            switch (LatestVersion.ComparePrecedenceTo(Compat.AppVersion))
            {
                case > 0:
                    // Newer version available
                    return true;
                case 0:
                {
                    // Same version available, check if we both have commit hash metadata
                    var updateHash = LatestVersion.Metadata;
                    var appHash = Compat.AppVersion.Metadata;

                    // Always assume update if (We don't have hash && Update has hash)
                    if (string.IsNullOrEmpty(appHash) && !string.IsNullOrEmpty(updateHash))
                    {
                        return true;
                    }

                    // Trim both to the lower length, to a minimum of 7 characters
                    var minLength = Math.Min(7, Math.Min(updateHash.Length, appHash.Length));
                    updateHash = updateHash[..minLength];
                    appHash = appHash[..minLength];

                    // If different, we can update
                    if (updateHash != appHash)
                    {
                        return true;
                    }

                    break;
                }
            }

            return false;
        }
    }
}
