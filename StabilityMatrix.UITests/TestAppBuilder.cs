using System.Collections;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.Extensions;
using Semver;
using StabilityMatrix.Avalonia;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Update;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Updater;
using StabilityMatrix.UITests;
using StabilityMatrix.UITests.Fakes;
using TheArtOfDev.HtmlRenderer.Adapters;
using TheArtOfDev.HtmlRenderer.Avalonia;

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
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .AfterSetup(_ => ClearHtmlRendererFontCache());
    }

    /// <summary>
    /// HtmlRenderer (behind MarkdownViewer) caches fonts in a process-wide static, while each
    /// headless test gets its own FontManager that is disposed when the test ends. A later test
    /// would then measure text with a freed HarfBuzz font and crash the test host, so the cache is
    /// emptied as each test's app is set up. The app itself has one FontManager for its lifetime.
    /// </summary>
    private static void ClearHtmlRendererFontCache()
    {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
        var adapter = typeof(HtmlControl)
            .Assembly.GetType("TheArtOfDev.HtmlRenderer.Avalonia.Adapters.AvaloniaAdapter")!
            .GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)!
            .GetValue(null)!;
        var fontsHandler = typeof(RAdapter).GetField("_fontsHandler", instance)!.GetValue(adapter)!;
        var fontsCache = (IDictionary)
            fontsHandler.GetType().GetField("_fontsCache", instance)!.GetValue(fontsHandler)!;
        fontsCache.Clear();
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
        var settingsManager = Substitute.ForPartsOf<SettingsManager>(NullLogger<SettingsManager>.Instance);
        settingsManager.Settings.Analytics.LastSeenConsentVersion = Compat.AppVersion;
        settingsManager.Settings.Analytics.LastSeenConsentAccepted = true;
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
                "IX5/CCXWJQG0oGkYWVnuF34gTqF/dJSrDrUd6fuNMYnncL39G3HSvkXrjvJvR18MA2rQNB5z13h3/qBSf9c7DA==",
        };

        var updateHelper = Substitute.For<IUpdateHelper>();
        updateHelper
            .Configure()
            .StartCheckingForUpdates()
            .Returns(Task.CompletedTask)
            .AndDoes(_ => EventManager.Instance.OnUpdateAvailable(mockUpdateInfo));

        serviceCollection.AddSingleton(updateHelper);

        var httpClientFactory = new StaticHttpClientFactory(
            new HttpClient(new StaticHttpMessageHandler()) { BaseAddress = new Uri("https://example.org") }
        );
        serviceCollection.AddSingleton<IHttpClientFactory>(httpClientFactory);

        serviceCollection.AddSingleton<ICivArchiveApiClient, TestCivArchiveApiClient>();
    }

    private sealed class StaticHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => httpClient;
    }

    private sealed class StaticHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(
                new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "# Test changelog",
                        System.Text.Encoding.UTF8,
                        "text/markdown"
                    ),
                }
            );
        }
    }
}
