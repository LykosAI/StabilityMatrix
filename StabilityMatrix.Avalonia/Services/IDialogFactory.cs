using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Avalonia.Services;

public interface IDialogFactory
{
    SelectModelVersionViewModel CreateSelectModelVersionViewModel(CivitModel model, ContentDialog dialog);
    OneClickInstallViewModel CreateOneClickInstallViewModel();
}