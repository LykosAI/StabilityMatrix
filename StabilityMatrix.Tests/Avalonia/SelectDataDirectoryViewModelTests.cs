using NSubstitute;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class SelectDataDirectoryViewModelTests
{
    [TestMethod]
    public void IsInOneDriveFolder_ReturnsTrue_WhenDataDirectoryIsInsideOneDriveFolderUnderUserProfile()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var viewModel = new SelectDataDirectoryViewModel(Substitute.For<ISettingsManager>())
        {
            DataDirectory = Path.Combine(userProfile, "OneDrive - Test", Guid.NewGuid().ToString()),
        };

        Assert.IsTrue(viewModel.IsInOneDriveFolder);
    }

    [TestMethod]
    public void IsInOneDriveFolder_ReturnsFalse_WhenDataDirectoryIsOutsideOneDriveFolder()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var viewModel = new SelectDataDirectoryViewModel(Substitute.For<ISettingsManager>())
        {
            DataDirectory = Path.Combine(userProfile, "Documents", Guid.NewGuid().ToString()),
        };

        Assert.IsFalse(viewModel.IsInOneDriveFolder);
    }
}
