using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.Extensions;
using Semver;
using StabilityMatrix.Avalonia;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Update;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Updater;
using StabilityMatrix.UITests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace StabilityMatrix.UITests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        ConfigureGlobals();

        Program.SetupAvaloniaApp();

        App.BeforeBuildServiceProvider += (_, x) => ConfigureAppServices(x);

        return AppBuilder
            .Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    private static void ConfigureGlobals()
    {
        var tempDir = TempDirFixture.ModuleTempDir;
        var globalSettings = Path.Combine(tempDir, "AppDataHome");

        Compat.SetAppDataHome(globalSettings);
    }

    private static void ConfigureAppServices(IServiceCollection serviceCollection)
    {
        // ISettingsManager
        var settingsManager = Substitute.ForPartsOf<SettingsManager>();
        serviceCollection.AddSingleton<ISettingsManager>(settingsManager);

        // IUpdateHelper
        var mockUpdateInfo = new UpdateInfo()
        {
            Version = SemVersion.Parse("2.999.0"),
            ReleaseDate = DateTimeOffset.UnixEpoch,
            Channel = UpdateChannel.Stable,
            Type = UpdateType.Normal,
            Url = new Uri("https://example.org"),
            Changelog = new Uri("https://example.org"),
            HashBlake3 = "46e11a5216c55d4c9d3c54385f62f3e1022537ae191615237f05e06d6f8690d0",
            Signature =
                "IX5/CCXWJQG0oGkYWVnuF34gTqF/dJSrDrUd6fuNMYnncL39G3HSvkXrjvJvR18MA2rQNB5z13h3/qBSf9c7DA=="
        };

        var updateHelper = Substitute.For<IUpdateHelper>();
        updateHelper
            .Configure()
            .StartCheckingForUpdates()
            .Returns(Task.CompletedTask)
            .AndDoes(_ => EventManager.Instance.OnUpdateAvailable(mockUpdateInfo));

        serviceCollection.AddSingleton(updateHelper);

        // UpdateViewModel
        var updateViewModel = Substitute.ForPartsOf<UpdateViewModel>(
            Substitute.For<ILogger<UpdateViewModel>>(),
            settingsManager,
            null,
            updateHelper
        );
        updateViewModel.Configure().GetReleaseNotes("").Returns("Test");

        serviceCollection.AddSingleton(updateViewModel);
    }
}
