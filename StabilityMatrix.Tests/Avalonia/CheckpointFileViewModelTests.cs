using Microsoft.Extensions.Logging;
using NSubstitute;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.CheckpointManager;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class CheckpointFileViewModelTests
{
    [TestMethod]
    public void HasStandardUpdate_IsFalse_WhenUpdateIsEarlyAccessOnly()
    {
        var vm = CreateViewModel(CreateCheckpointFile(hasUpdate: true, hasEarlyAccessUpdateOnly: true));

        Assert.IsTrue(vm.HasEarlyAccessUpdateOnly);
        Assert.IsFalse(vm.HasStandardUpdate);
    }

    [TestMethod]
    public void CheckpointFile_Setter_RaisesDerivedUpdatePropertyNotifications()
    {
        var vm = CreateViewModel(CreateCheckpointFile(hasUpdate: true, hasEarlyAccessUpdateOnly: true));
        var changed = new List<string>();

        vm.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.PropertyName))
            {
                changed.Add(e.PropertyName!);
            }
        };

        vm.CheckpointFile = CreateCheckpointFile(hasUpdate: true, hasEarlyAccessUpdateOnly: false);

        Assert.IsFalse(vm.HasEarlyAccessUpdateOnly);
        Assert.IsTrue(vm.HasStandardUpdate);
        CollectionAssert.Contains(changed, nameof(CheckpointFileViewModel.HasEarlyAccessUpdateOnly));
        CollectionAssert.Contains(changed, nameof(CheckpointFileViewModel.HasStandardUpdate));
    }

    private static CheckpointFileViewModel CreateViewModel(LocalModelFile checkpointFile)
    {
        var settingsManager = Substitute.For<ISettingsManager>();
        settingsManager.Settings.Returns(new Settings { ShowNsfwInCheckpointsPage = true });
        settingsManager.IsLibraryDirSet.Returns(false);

        return new CheckpointFileViewModel(
            settingsManager,
            Substitute.For<IModelIndexService>(),
            Substitute.For<INotificationService>(),
            Substitute.For<IDownloadService>(),
            Substitute.For<IServiceManager<ViewModelBase>>(),
            Substitute.For<ILogger>(),
            checkpointFile
        );
    }

    private static LocalModelFile CreateCheckpointFile(bool hasUpdate, bool hasEarlyAccessUpdateOnly)
    {
        return new LocalModelFile
        {
            RelativePath = "StableDiffusion/test-vm.safetensors",
            SharedFolderType = SharedFolderType.StableDiffusion,
            HasUpdate = hasUpdate,
            HasEarlyAccessUpdateOnly = hasEarlyAccessUpdateOnly,
            ConnectedModelInfo = new ConnectedModelInfo
            {
                ModelId = 77,
                VersionId = 700,
                Source = ConnectedModelSource.Civitai,
                ModelName = "VM Test Model",
                ModelDescription = string.Empty,
                VersionName = "v700",
                Tags = [],
                Hashes = new CivitFileHashes(),
            },
        };
    }
}
