using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;
using StabilityMatrix.Avalonia.ViewModels.CheckpointManager;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Updater;

namespace StabilityMatrix.Avalonia.DesignData;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public static class DesignData
{
    [NotNull] public static IServiceProvider? Services { get; set; }

    private static bool isInitialized;

    // This needs to be static method instead of static constructor
    // or else Avalonia analyzers won't work.
    public static void Initialize()
    {
        if (isInitialized)
            throw new InvalidOperationException("DesignData is already initialized.");

        var services = new ServiceCollection();

        var activePackageId = Guid.NewGuid();
        services.AddSingleton<ISettingsManager, MockSettingsManager>(_ => new MockSettingsManager
        {
            Settings =
            {
                InstalledPackages = new List<InstalledPackage>
                {
                    new()
                    {
                        Id = activePackageId,
                        DisplayName = "My Installed Package",
                        DisplayVersion = "v1.0.0",
                        PackageName = "stable-diffusion-webui",
                        PackageVersion = "v1.0.0",
                        LibraryPath = $"Packages{Path.DirectorySeparatorChar}example-webui",
                        LastUpdateCheck = DateTimeOffset.Now
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        DisplayName = "Comfy Diffusion WebUI Dev Branch Long Name",
                        PackageName = "ComfyUI",
                        DisplayVersion = "main@ab73d4a",
                        LibraryPath = $"Packages{Path.DirectorySeparatorChar}example-webui",
                        LastUpdateCheck = DateTimeOffset.Now
                    }
                },
                ActiveInstalledPackageId = activePackageId
            }
        });

        // General services
        services.AddLogging()
            .AddSingleton<INavigationService, NavigationService>()
            .AddSingleton<IPackageFactory, PackageFactory>()
            .AddSingleton<IUpdateHelper, UpdateHelper>()
            .AddSingleton<ModelFinder>()
            .AddSingleton<SharedState>();

        // Mock services
        services
            .AddSingleton<INotificationService, MockNotificationService>()
            .AddSingleton<ISharedFolders, MockSharedFolders>()
            .AddSingleton<IDownloadService, MockDownloadService>()
            .AddSingleton<IHttpClientFactory, MockHttpClientFactory>()
            .AddSingleton<IDiscordRichPresenceService, MockDiscordRichPresenceService>()
            .AddSingleton<IModelIndexService, MockModelIndexService>()
            .AddSingleton<ITrackedDownloadService, MockTrackedDownloadService>();

        // Placeholder services that nobody should need during design time
        services
            .AddSingleton<IPyRunner>(_ => null!)
            .AddSingleton<ILiteDbContext>(_ => null!)
            .AddSingleton<ICivitApi>(_ => null!)
            .AddSingleton<IGithubApiCache>(_ => null!)
            .AddSingleton<IPrerequisiteHelper>(_ => null!);

        // Using some default service implementations from App
        App.ConfigurePackages(services);
        App.ConfigurePageViewModels(services);
        App.ConfigureDialogViewModels(services);
        App.ConfigureViews(services);

        // Override Launch page with mock
        services.Remove(ServiceDescriptor.Singleton<LaunchPageViewModel, LaunchPageViewModel>());
        services.AddSingleton<LaunchPageViewModel, MockLaunchPageViewModel>();

        Services = services.BuildServiceProvider();

        var dialogFactory = Services.GetRequiredService<ServiceManager<ViewModelBase>>();
        var settingsManager = Services.GetRequiredService<ISettingsManager>();
        var downloadService = Services.GetRequiredService<IDownloadService>();
        var modelFinder = Services.GetRequiredService<ModelFinder>();
        var packageFactory = Services.GetRequiredService<IPackageFactory>();
        var notificationService = Services.GetRequiredService<INotificationService>();

        LaunchOptionsViewModel = Services.GetRequiredService<LaunchOptionsViewModel>();
        LaunchOptionsViewModel.Cards = new[]
        {
            LaunchOptionCard.FromDefinition(new LaunchOptionDefinition
            {
                Name = "Host",
                Type = LaunchOptionType.String,
                Description = "The host name for the Web UI",
                DefaultValue = "localhost",
                Options = {"--host"}
            }),
            LaunchOptionCard.FromDefinition(new LaunchOptionDefinition
            {
                Name = "API",
                Type = LaunchOptionType.Bool,
                Options = {"--api"}
            })
        };
        LaunchOptionsViewModel.UpdateFilterCards();

        InstallerViewModel = Services.GetRequiredService<InstallerViewModel>();
        InstallerViewModel.AvailablePackages =
            packageFactory.GetAllAvailablePackages().ToImmutableArray();
        InstallerViewModel.SelectedPackage = InstallerViewModel.AvailablePackages[0];
        InstallerViewModel.ReleaseNotes = "## Release Notes\nThis is a test release note.";

        // Checkpoints page
        CheckpointsPageViewModel.CheckpointFolders = new ObservableCollection<CheckpointFolder>
        {
            new(settingsManager, downloadService, modelFinder)
            {
                Title = "StableDiffusion",
                DirectoryPath = "Models/StableDiffusion",
                CheckpointFiles = new AdvancedObservableList<CheckpointFile>
                {
                    new()
                    {
                        FilePath = "~/Models/StableDiffusion/electricity-light.safetensors",
                        Title = "Auroral Background",
                        PreviewImagePath = "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/" +
                                           "78fd2a0a-42b6-42b0-9815-81cb11bb3d05/00009-2423234823.jpeg",
                        ConnectedModel = new ConnectedModelInfo
                        {
                            VersionName = "Lightning Auroral",
                            BaseModel = "SD 1.5",
                            ModelName = "Auroral Background",
                            ModelType = CivitModelType.Model,
                            FileMetadata = new CivitFileMetadata
                            {
                                Format = CivitModelFormat.SafeTensor,
                                Fp = CivitModelFpType.fp16,
                                Size = CivitModelSize.pruned,
                            }
                        }
                    },
                    new()
                    {
                        FilePath = "~/Models/Lora/model.safetensors",
                        Title = "Some model"
                    },
                },
            },
            new(settingsManager, downloadService, modelFinder)
            {
                Title = "Lora",
                DirectoryPath = "Packages/Lora",
                SubFolders = new AdvancedObservableList<CheckpointFolder>()
                {
                    new(settingsManager, downloadService, modelFinder)
                    {
                        Title = "StableDiffusion",
                        DirectoryPath = "Packages/Lora/Subfolder",
                    },
                    new(settingsManager, downloadService, modelFinder)
                    {
                        Title = "Lora",
                        DirectoryPath = "Packages/StableDiffusion/Subfolder",
                    }
                },
                CheckpointFiles = new AdvancedObservableList<CheckpointFile>
                {
                    new()
                    {
                        FilePath = "~/Models/Lora/lora_v2.pt",
                        Title = "Best Lora v2",
                    }
                }
            }
        };

        foreach (var folder in CheckpointsPageViewModel.CheckpointFolders)
        {
            folder.DisplayedCheckpointFiles = folder.CheckpointFiles;
        }

        CheckpointBrowserViewModel.ModelCards = new
            ObservableCollection<CheckpointBrowserCardViewModel>
            {
                dialogFactory.Get<CheckpointBrowserCardViewModel>(vm =>
                {
                    vm.CivitModel = new CivitModel
                    {
                        Name = "BB95 Furry Mix",
                        Description = "A furry mix of BB95",
                    };
                })
            };

        NewCheckpointsPageViewModel.AllCheckpoints = new ObservableCollection<CheckpointFile>
        {
            new()
            {
                FilePath = "~/Models/StableDiffusion/electricity-light.safetensors",
                Title = "Auroral Background",
                PreviewImagePath = "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/" +
                                   "78fd2a0a-42b6-42b0-9815-81cb11bb3d05/00009-2423234823.jpeg",
                ConnectedModel = new ConnectedModelInfo
                {
                    VersionName = "Lightning Auroral",
                    BaseModel = "SD 1.5",
                    ModelName = "Auroral Background",
                    ModelType = CivitModelType.Model,
                    FileMetadata = new CivitFileMetadata
                    {
                        Format = CivitModelFormat.SafeTensor,
                        Fp = CivitModelFpType.fp16,
                        Size = CivitModelSize.pruned,
                    }
                }
            },
            new()
            {
                FilePath = "~/Models/Lora/model.safetensors",
                Title = "Some model"
            }
        };

        ProgressManagerViewModel.ProgressItems.AddRange(new ProgressItemViewModelBase[]
        {
            new ProgressItemViewModel(new ProgressItem(Guid.NewGuid(), "Test File.exe",
                new ProgressReport(0.5f, "Downloading..."))),
            new MockDownloadProgressItemViewModel("Test File 2.exe"),
        });

        UpdateViewModel = Services.GetRequiredService<UpdateViewModel>();
        UpdateViewModel.UpdateText =
            $"Stability Matrix v2.0.1 is now available! You currently have v2.0.0. Would you like to update now?";
        UpdateViewModel.ReleaseNotes =
            "## v2.0.1\n- Fixed a bug\n- Added a feature\n- Removed a feature";

        isInitialized = true;
    }

    [NotNull] public static InstallerViewModel? InstallerViewModel { get; private set; }
    [NotNull] public static LaunchOptionsViewModel? LaunchOptionsViewModel { get; private set; }
    [NotNull] public static UpdateViewModel? UpdateViewModel { get; private set; }

    public static ServiceManager<ViewModelBase> DialogFactory =>
        Services.GetRequiredService<ServiceManager<ViewModelBase>>();

    public static MainWindowViewModel MainWindowViewModel =>
        Services.GetRequiredService<MainWindowViewModel>();

    public static FirstLaunchSetupViewModel FirstLaunchSetupViewModel =>
        Services.GetRequiredService<FirstLaunchSetupViewModel>();

    public static LaunchPageViewModel LaunchPageViewModel =>
        Services.GetRequiredService<LaunchPageViewModel>();

    public static PackageManagerViewModel PackageManagerViewModel
    {
        get
        {
            var settings = Services.GetRequiredService<ISettingsManager>();
            var vm = Services.GetRequiredService<PackageManagerViewModel>();

            vm.SetPackages(settings.Settings.InstalledPackages);
            vm.SetUnknownPackages(new InstalledPackage[]
            {
                UnknownInstalledPackage.FromDirectoryName("sd-unknown-with-long-name"),
            });
            
            vm.PackageCards[0].IsUpdateAvailable = true;
            
            return vm;
        }
    }

    public static CheckpointsPageViewModel CheckpointsPageViewModel =>
        Services.GetRequiredService<CheckpointsPageViewModel>();

    public static NewCheckpointsPageViewModel NewCheckpointsPageViewModel =>
        Services.GetRequiredService<NewCheckpointsPageViewModel>();

    public static SettingsViewModel SettingsViewModel =>
        Services.GetRequiredService<SettingsViewModel>();

    public static CheckpointBrowserViewModel CheckpointBrowserViewModel =>
        Services.GetRequiredService<CheckpointBrowserViewModel>();

    public static SelectModelVersionViewModel SelectModelVersionViewModel =>
        DialogFactory.Get<SelectModelVersionViewModel>(vm =>
        {
            // Sample data
            var sampleCivitVersions = new List<CivitModelVersion>
            {
                new()
                {
                    Name = "BB95 Furry Mix",
                    Description = @"Introducing SnoutMix
A Mix of non-Furry and Furry models such as Furtastic and BB95Furry to create a great variety of anthro AI generation options, but bringing out more detail, still giving a lot of freedom to customise the human aspects, and having great backgrounds, with a focus on something more realistic. Works well with realistic character loras.
The gallery images are often inpainted, but you will get something very similar if copying their data directly. They are inpainted using the same model, therefore all results are possible without anything custom/hidden-away. Controlnet Tiled is applied to enhance them further afterwards. Gallery images were made with same model but before it was renamed",
                    BaseModel = "SD 1.5",
                    Files = new List<CivitFile>
                    {
                        new()
                        {
                            Name = "bb95-v100-uwu-reallylongfilename-v1234576802.safetensors",
                            Type = CivitFileType.Model,
                            Metadata = new CivitFileMetadata
                            {
                                Format = CivitModelFormat.SafeTensor,
                                Fp = CivitModelFpType.fp16,
                                Size = CivitModelSize.pruned
                            }
                        },
                        new()
                        {
                            Name = "bb95-v100-uwu-reallylongfilename-v1234576802-fp32.safetensors",
                            Type = CivitFileType.Model,
                            Metadata = new CivitFileMetadata
                            {
                                Format = CivitModelFormat.SafeTensor,
                                Fp = CivitModelFpType.fp32,
                                Size = CivitModelSize.full
                            },
                            Hashes = new CivitFileHashes
                            {
                                BLAKE3 = "ABCD"
                            }
                        }
                    }
                }
            };
            var sampleViewModel =
                new ModelVersionViewModel(new HashSet<string> {"ABCD"}, sampleCivitVersions[0]);

            // Sample data for dialogs
            vm.Versions = new List<ModelVersionViewModel> {sampleViewModel};
            vm.Title = sampleCivitVersions[0].Name;
            vm.Description = sampleCivitVersions[0].Description;
            vm.SelectedVersionViewModel = sampleViewModel;
        });

    public static OneClickInstallViewModel OneClickInstallViewModel =>
        Services.GetRequiredService<OneClickInstallViewModel>();

    public static SelectDataDirectoryViewModel SelectDataDirectoryViewModel =>
        Services.GetRequiredService<SelectDataDirectoryViewModel>();

    public static ProgressManagerViewModel ProgressManagerViewModel =>
        Services.GetRequiredService<ProgressManagerViewModel>();

    public static ExceptionViewModel ExceptionViewModel =>
        DialogFactory.Get<ExceptionViewModel>(viewModel =>
        {
            // Use try-catch to generate traceback information
            try
            {
                try
                {
                    throw new OperationCanceledException("Example");
                }
                catch (OperationCanceledException e)
                {
                    throw new AggregateException(e);
                }
            }
            catch (AggregateException e)
            {
                viewModel.Exception = e;
            }
        });

    public static EnvVarsViewModel EnvVarsViewModel => DialogFactory.Get<EnvVarsViewModel>(
        viewModel =>
        {
            viewModel.EnvVars = new ObservableCollection<EnvVarKeyPair>
            {
                new("UWU", "TRUE"),
            };
        });

    public static PackageImportViewModel PackageImportViewModel =>
        DialogFactory.Get<PackageImportViewModel>();

    public static RefreshBadgeViewModel RefreshBadgeViewModel => new()
    {
        State = ProgressState.Success
    };
}
