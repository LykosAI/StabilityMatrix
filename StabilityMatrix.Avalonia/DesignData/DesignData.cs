using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels;
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
        if (isInitialized) throw new InvalidOperationException("DesignData is already initialized.");
        
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
                        PackageName = "stable-diffusion-webui",
                        PackageVersion = "v1.0.0",
                        LibraryPath = $"Packages{Environment.NewLine}example-webui",
                        LastUpdateCheck = DateTimeOffset.Now
                    }
                },
                ActiveInstalledPackage = activePackageId
            }
        });
        
        // General services
        services.AddLogging()
            .AddSingleton<IPackageFactory, PackageFactory>()
            .AddSingleton<IUpdateHelper, UpdateHelper>()
            .AddSingleton<ModelFinder>()
            .AddSingleton<SharedState>();
        
        // Mock services
        services
            .AddSingleton<INotificationService, MockNotificationService>()
            .AddSingleton<ISharedFolders, MockSharedFolders>()
            .AddSingleton<IDownloadService, MockDownloadService>()
            .AddSingleton<IHttpClientFactory, MockHttpClientFactory>();
        
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
                Options = { "--host" }
            }),
            LaunchOptionCard.FromDefinition(new LaunchOptionDefinition
            {
                Name = "API",
                Type = LaunchOptionType.Bool,
                Options = { "--api" }
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
                Title = "Lora",
                DirectoryPath = "Packages/lora",
                CheckpointFiles = new ObservableCollection<CheckpointFile>
                {
                    new()
                    {
                        FilePath = "~/Models/Lora/electricity-light.safetensors",
                        Title = "Auroral Background",
                        PreviewImagePath = "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/" +
                                           "78fd2a0a-42b6-42b0-9815-81cb11bb3d05/00009-2423234823.jpeg",
                        ConnectedModel = new ConnectedModelInfo
                        {
                            VersionName = "Lightning Auroral",
                            BaseModel = "SD 1.5",
                            ModelName = "Auroral Background",
                            ModelType = CivitModelType.LORA,
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
                Title = "VAE",
                DirectoryPath = "Packages/VAE",
                CheckpointFiles = new ObservableCollection<CheckpointFile>
                {
                    new()
                    {
                        FilePath = "~/Models/VAE/vae_v2.pt",
                        Title = "VAE v2",
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
                new(new CivitModel
                    {
                        Name = "BB95 Furry Mix",
                        Description = "A furry mix of BB95",
                    }, downloadService, settingsManager,
                    dialogFactory, notificationService)
            };
        
        ProgressManagerViewModel.ProgressItems = new ObservableCollection<ProgressItemViewModel>
        {
            new(new ProgressItem(Guid.NewGuid(), "Test File.exe", new ProgressReport(0.5f, "Downloading..."))),
            new(new ProgressItem(Guid.NewGuid(), "Test File 2.uwu", new ProgressReport(0.25f, "Extracting...")))
        };

        UpdateViewModel = Services.GetRequiredService<UpdateViewModel>();
        UpdateViewModel.UpdateText =
            $"Stability Matrix v2.0.1 is now available! You currently have v2.0.0. Would you like to update now?";
        UpdateViewModel.ReleaseNotes = "## v2.0.1\n- Fixed a bug\n- Added a feature\n- Removed a feature";
        
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
    public static PackageManagerViewModel PackageManagerViewModel => 
        Services.GetRequiredService<PackageManagerViewModel>();
    public static CheckpointsPageViewModel CheckpointsPageViewModel => 
        Services.GetRequiredService<CheckpointsPageViewModel>();
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
                    Description = "v1.0.0",
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
            vm.Versions = new List<ModelVersionViewModel>{sampleViewModel};
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

    public static RefreshBadgeViewModel RefreshBadgeViewModel => new()
    {
        State = ProgressState.Success
    };
}
