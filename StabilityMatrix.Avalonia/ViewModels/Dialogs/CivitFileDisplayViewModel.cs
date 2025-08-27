using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public class CivitFileDisplayViewModel
{
    public required CivitModelVersion ModelVersion { get; init; }
    public required CivitFileViewModel FileViewModel { get; init; }
}
