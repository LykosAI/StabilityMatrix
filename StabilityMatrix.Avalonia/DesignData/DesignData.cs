using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AvaloniaEdit.Utils;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.DesignData;

public static class DesignData
{
    static DesignData()
    {
        var settingsManager = new MockSettingsManager
        {
            Settings =
            {
                InstalledPackages = new List<InstalledPackage>
                {
                    new()
                    {
                        DisplayName = "My Installed Package",
                        PackageName = "stable-diffusion-webui",
                        PackageVersion = "v1.0.0",
                        LibraryPath = "Packages\\example-webui",
                        LastUpdateCheck = DateTimeOffset.Now
                    }
                },
                ActiveInstalledPackage = new Guid()
            }
        };
        var packages = new List<BasePackage>
        {
            new A3WebUI(null!, settingsManager, null!, null!),
            new ComfyUI(null!, settingsManager, null!, null!)
        };
        var packageFactory = new PackageFactory(packages);
        var notificationService = new MockNotificationService();
        var sharedFolders = new SharedFolders(settingsManager, packageFactory);
        var downloadService = new MockDownloadService();
        var modelFinder = new ModelFinder(null!, null!);
        
        LaunchPageViewModel = new LaunchPageViewModel(
            null!, settingsManager, packageFactory, new PyRunner(), notificationService);
        
        LaunchPageViewModel.InstalledPackages.AddRange(settingsManager.Settings.InstalledPackages);
        LaunchPageViewModel.SelectedPackage = settingsManager.Settings.InstalledPackages[0];
        
        PackageManagerViewModel = new PackageManagerViewModel(null!, settingsManager, packageFactory, notificationService);
        SettingsViewModel = new SettingsViewModel(notificationService);

        SelectModelVersionViewModel = new SelectModelVersionViewModel(new CivitModel
        {
            Name = "BB95 Furry Mix",
            Nsfw = false,
            ModelVersions = new List<CivitModelVersion>
            {
                new()
                {
                    Name = "BB95 Furry Mix",
                    Description = "v1.0.0",
                }
            }
        }, null!, settingsManager, downloadService);

        InstallerViewModel = new InstallerViewModel(settingsManager, null!,
            downloadService, notificationService, sharedFolders, null!)
        {
            AvailablePackages = new ObservableCollection<BasePackage>(packageFactory.GetAllAvailablePackages()),
            SelectedPackage = packages[0]
        };
        
        // Checkpoints page
        CheckpointsPageViewModel = new CheckpointsPageViewModel(
            sharedFolders, settingsManager, downloadService, modelFinder)
        {
            CheckpointFolders = new ObservableCollection<CheckpointFolder>
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
                    }
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
            }
        };

        CheckpointBrowserViewModel =
            new CheckpointBrowserViewModel(null!, downloadService, settingsManager, null!, null!,
                notificationService)
            {
                ModelCards = new ObservableCollection<CheckpointBrowserCardViewModel>
                {
                    new(new CivitModel
                        {
                            Name = "BB95 Furry Mix",
                            Description = "A furry mix of BB95",
                        }, downloadService, settingsManager,
                        new DialogFactory(settingsManager, downloadService, packageFactory, null!,
                            null!, null!, sharedFolders), notificationService)
                }
            };
        
        // Main window
        MainWindowViewModel = new MainWindowViewModel(settingsManager, null!)
        {
            Pages = new List<PageViewModelBase>
            {
                LaunchPageViewModel,
                PackageManagerViewModel,
                CheckpointBrowserViewModel
            },
            FooterPages = new List<PageViewModelBase>
            {
                SettingsViewModel
            }
        };

        OneClickInstallViewModel = new OneClickInstallViewModel(settingsManager, packageFactory,
            null!, null!, null!, sharedFolders);
    }
    
    public static MainWindowViewModel MainWindowViewModel { get; }
    public static LaunchPageViewModel LaunchPageViewModel { get; }
    public static PackageManagerViewModel PackageManagerViewModel { get; }
    public static CheckpointsPageViewModel CheckpointsPageViewModel { get; }
    public static SettingsViewModel SettingsViewModel { get; }
    public static CheckpointBrowserViewModel CheckpointBrowserViewModel { get; }
    public static SelectModelVersionViewModel SelectModelVersionViewModel { get; }
    public static OneClickInstallViewModel OneClickInstallViewModel { get; }
    public static InstallerViewModel InstallerViewModel { get; }
}
