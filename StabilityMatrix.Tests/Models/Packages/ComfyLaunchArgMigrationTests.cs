using NSubstitute;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Services.Rocm;

namespace StabilityMatrix.Tests.Models.Packages;

[TestClass]
public class ComfyLaunchArgMigrationTests
{
    [TestMethod]
    public void NormalizeLaunchArguments_StripsObsoleteNormalVramAndPersistsUpdatedArgs()
    {
        var settingsManager = Substitute.For<ISettingsManager>();
        var package = new TestComfyUI(
            Substitute.For<IGithubApiCache>(),
            settingsManager,
            Substitute.For<IDownloadService>(),
            Substitute.For<IPrerequisiteHelper>(),
            Substitute.For<IPyInstallationManager>(),
            Substitute.For<IPipWheelService>(),
            Substitute.For<IRocmPackageHelper>()
        );

        var installedPackage = new InstalledPackage
        {
            Id = Guid.NewGuid(),
            PackageName = "ComfyUI",
            LaunchArgs =
            [
                new LaunchOption
                {
                    Name = "--normalvram",
                    Type = LaunchOptionType.Bool,
                    OptionValue = true,
                },
            ],
        };

        var fallbackArguments = ProcessArgs.FromQuoted(
            installedPackage
                .LaunchArgs.Select(option => option.ToArgString())
                .Where(argument => argument is not null)
                .Select(argument => argument!)
        );

        var normalizedArguments = package.Normalize(installedPackage, fallbackArguments);

        Assert.IsFalse(installedPackage.LaunchArgs.Any(option => option.Name == "--normalvram"));
        Assert.IsFalse(normalizedArguments.Contains("--normalvram"));
        settingsManager
            .Received(1)
            .SaveLaunchArgs(
                installedPackage.Id,
                Arg.Is<IEnumerable<LaunchOption>>(options =>
                    options.All(option => option.Name != "--normalvram")
                )
            );
    }

    private sealed class TestComfyUI : ComfyUI
    {
        public TestComfyUI(
            IGithubApiCache githubApi,
            ISettingsManager settingsManager,
            IDownloadService downloadService,
            IPrerequisiteHelper prerequisiteHelper,
            IPyInstallationManager pyInstallationManager,
            IPipWheelService pipWheelService,
            IRocmPackageHelper rocmPackageHelper
        )
            : base(
                githubApi,
                settingsManager,
                downloadService,
                prerequisiteHelper,
                pyInstallationManager,
                pipWheelService,
                rocmPackageHelper
            ) { }

        public ProcessArgs Normalize(InstalledPackage installedPackage, ProcessArgs fallbackArguments) =>
            NormalizeLaunchArguments(installedPackage, fallbackArguments);
    }
}
