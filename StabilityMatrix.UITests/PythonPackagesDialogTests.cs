using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.UITests;

/// <summary>
/// The Python Packages dialog loads a package's details (pip show / pip index) whenever the
/// selection changes, and searching moves the selection on almost every keystroke.
/// </summary>
public class PythonPackagesDialogTests
{
    private static readonly string[] PackageNames =
    [
        "absl-py",
        "accelerate",
        "aiohttp",
        "certifi",
        "einops",
        "filelock",
        "idna",
        "jax",
        "jaxlib",
        "jinja2",
        "jsonmerge",
        "jsonschema",
        "kornia",
        "lark",
        "ml-dtypes",
        "numpy",
        "opt-einsum",
        "pillow",
        "pip",
        "safetensors",
        "scipy",
        "torch",
        "torchvision",
        "transformers",
    ];

    [AvaloniaFact]
    public async Task TypingInSearch_WhenPackageDetailsFailToLoad_DoesNotCrash()
    {
        var dialog = await PackagesDialog.OpenAsync(pipShow: name =>
            name.StartsWith('j')
                ? throw new FileNotFoundException("uv not found")
                : Task.FromResult<PipShowResult?>(new PipShowResult { Name = name, Version = "1.0.0" })
        );

        // Any exception escaping the details load is rethrown on the UI thread, which ends the app
        await dialog.TypeSearchAsync("j", "ja", "jax", "ja", "j", "");

        Assert.Equal("jax", dialog.Vm.SelectedPackage?.Package.Name);
        Assert.All(dialog.Vm.Packages, p => Assert.False(p.IsLoading));
        dialog.Close();
    }

    [AvaloniaFact]
    public async Task TypingInSearch_LoadsOnePackagesDetailsAtATime()
    {
        var running = 0;
        var maxRunning = 0;
        var dialog = await PackagesDialog.OpenAsync(pipShow: async name =>
        {
            maxRunning = Math.Max(maxRunning, ++running);
            await Task.Delay(150);
            running--;
            return new PipShowResult { Name = name, Version = "1.0.0" };
        });

        await dialog.TypeSearchAsync("j", "js", "t", "to", "a");
        await dialog.SettleAsync(rounds: 20);

        Assert.Equal(1, maxRunning);
        Assert.NotNull(dialog.Vm.SelectedPackage?.PipShowResult);
        dialog.Close();
    }

    private sealed class PackagesDialog
    {
        private readonly Window window;
        private readonly TextBox searchBox;

        public PythonPackagesViewModel Vm { get; }

        private PackagesDialog(PythonPackagesViewModel vm, Window window, TextBox searchBox)
        {
            Vm = vm;
            this.window = window;
            this.searchBox = searchBox;
        }

        public static async Task<PackagesDialog> OpenAsync(Func<string, Task<PipShowResult?>> pipShow)
        {
            var runner = Substitute.For<IPyVenvRunner>();
            runner.PipList().Returns(PackageNames.Select(n => new PipPackageInfo(n, "1.0.0")).ToList());
            runner.PipShow(Arg.Any<string>()).Returns(call => pipShow(call.Arg<string>()));
            runner
                .PipIndex(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>())
                .Returns(new PipIndexResult { AvailableVersions = ["1.0.0"] });

            var vm = new PythonPackagesViewModel(
                NullLogger<PythonPackagesViewModel>.Instance,
                Substitute.For<ISettingsManager>(),
                Substitute.For<IPyInstallationManager>(),
                Substitute.For<IPrerequisiteHelper>()
            )
            {
                VenvPath = new DirectoryPath(Path.GetTempPath(), "venv"),
                VenvRunnerFactory = () => Task.FromResult(runner),
            };

            var view = new PythonPackagesDialog { DataContext = vm };
            var window = new Window
            {
                Width = 1000,
                Height = 700,
                Content = view,
            };
            window.Show();
            await vm.OnLoadedAsync();

            var dialog = new PackagesDialog(
                vm,
                window,
                view.GetVisualDescendants().OfType<TextBox>().First()
            );
            await dialog.SettleAsync();
            return dialog;
        }

        public async Task TypeSearchAsync(params string[] texts)
        {
            foreach (var text in texts)
            {
                searchBox.Text = text;
                await SettleAsync();
            }
        }

        public async Task SettleAsync(int rounds = 6)
        {
            for (var i = 0; i < rounds; i++)
            {
                await Task.Delay(50);
                Dispatcher.UIThread.RunJobs();
            }
        }

        public void Close() => window.Close();
    }
}
