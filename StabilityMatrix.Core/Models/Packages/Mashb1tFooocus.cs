﻿using Injectio.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[RegisterSingleton<BasePackage, Mashb1tFooocus>(Duplicate = DuplicateStrategy.Append)]
public class Mashb1tFooocus(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager
) : Fooocus(githubApi, settingsManager, downloadService, prerequisiteHelper, pyInstallationManager)
{
    public override string Name => "mashb1t-fooocus";
    public override string Author => "mashb1t";
    public override string RepositoryName => "Fooocus";
    public override string DisplayName { get; set; } = "Fooocus - mashb1t's 1-Up Edition";

    public override string Blurb =>
        "The purpose of this fork is to add new features / fix bugs and contribute back to Fooocus.";

    public override string LicenseUrl => "https://github.com/mashb1t/Fooocus/blob/main/LICENSE";

    public override bool ShouldIgnoreReleases => false;
}
