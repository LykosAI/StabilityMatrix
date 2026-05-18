using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class SettingsManagerTests
{
    private string? tempDirectory;

    [TestCleanup]
    public void Cleanup()
    {
        if (tempDirectory is not null && Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void TryFindLibrary_StripsObsoleteComfyNormalVramLaunchArgsAndPersists()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        var settingsPath = Path.Combine(tempDirectory, "settings.json");
        File.WriteAllText(
            settingsPath,
            """
            {
              "Version": 1,
              "InstalledPackages": [
                {
                  "Id": "11111111-1111-1111-1111-111111111111",
                  "PackageName": "ComfyUI",
                  "LaunchArgs": [
                    {
                      "Name": "--normalvram",
                      "Type": "Bool",
                      "OptionValue": true
                    },
                    {
                      "Name": "--lowvram",
                      "Type": "Bool",
                      "OptionValue": false
                    }
                  ]
                },
                {
                  "Id": "22222222-2222-2222-2222-222222222222",
                  "PackageName": "ComfyUI-Zluda",
                  "LaunchArgs": [
                    {
                      "Name": "--normalvram",
                      "Type": "Bool",
                      "OptionValue": true
                    }
                  ]
                },
                {
                  "Id": "33333333-3333-3333-3333-333333333333",
                  "PackageName": "OtherPackage",
                  "LaunchArgs": [
                    {
                      "Name": "--normalvram",
                      "Type": "Bool",
                      "OptionValue": true
                    }
                  ]
                }
              ]
            }
            """
        );

        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance);
        settingsManager.SetLibraryDirOverride(tempDirectory);

        var wasFound = settingsManager.TryFindLibrary();

        Assert.IsTrue(wasFound);

        var comfyPackage = settingsManager.Settings.InstalledPackages.Single(package =>
            package.PackageName == "ComfyUI"
        );
        var zludaPackage = settingsManager.Settings.InstalledPackages.Single(package =>
            package.PackageName == "ComfyUI-Zluda"
        );
        var otherPackage = settingsManager.Settings.InstalledPackages.Single(package =>
            package.PackageName == "OtherPackage"
        );

        Assert.IsFalse(comfyPackage.LaunchArgs!.Any(option => option.Name == "--normalvram"));
        Assert.IsFalse(zludaPackage.LaunchArgs!.Any(option => option.Name == "--normalvram"));
        Assert.IsTrue(otherPackage.LaunchArgs!.Any(option => option.Name == "--normalvram"));

        var persistedSettings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsPath));

        Assert.IsNotNull(persistedSettings);

        var persistedComfyPackage = persistedSettings.InstalledPackages.Single(package =>
            package.PackageName == "ComfyUI"
        );
        var persistedZludaPackage = persistedSettings.InstalledPackages.Single(package =>
            package.PackageName == "ComfyUI-Zluda"
        );
        var persistedOtherPackage = persistedSettings.InstalledPackages.Single(package =>
            package.PackageName == "OtherPackage"
        );

        Assert.IsFalse(persistedComfyPackage.LaunchArgs!.Any(option => option.Name == "--normalvram"));
        Assert.IsFalse(persistedZludaPackage.LaunchArgs!.Any(option => option.Name == "--normalvram"));
        Assert.IsTrue(persistedOtherPackage.LaunchArgs!.Any(option => option.Name == "--normalvram"));
    }
}
