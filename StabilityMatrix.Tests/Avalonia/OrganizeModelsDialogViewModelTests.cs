using NSubstitute;
using StabilityMatrix.Avalonia.Models.CheckpointOrganizer;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class OrganizeModelsDialogViewModelTests
{
    [TestMethod]
    public void ScanForMetadataCommand_RequestsMissingMetadataScan()
    {
        var viewModel = CreateViewModel();

        viewModel.ScanForMetadataCommand.Execute(null);

        Assert.AreEqual(ModelOrganizationMetadataAction.ScanMissing, viewModel.RequestedMetadataAction);
    }

    [TestMethod]
    public void UpdateMetadataCommand_RequestsUpdateExistingMetadata()
    {
        var viewModel = CreateViewModel();

        viewModel.UpdateMetadataCommand.Execute(null);

        Assert.AreEqual(ModelOrganizationMetadataAction.UpdateExisting, viewModel.RequestedMetadataAction);
    }

    private static OrganizeModelsDialogViewModel CreateViewModel()
    {
        return new OrganizeModelsDialogViewModel(
            Substitute.For<ISettingsManager>(),
            new ModelOrganizationService()
        );
    }
}
