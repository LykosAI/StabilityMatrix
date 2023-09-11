using StabilityMatrix.Avalonia.Diagnostics.LogViewer.Core.Logging;

namespace StabilityMatrix.Avalonia.Diagnostics.LogViewer.Core.ViewModels;

public class LogViewerControlViewModel : ViewModel, ILogDataStoreImpl
{
    #region Constructor

    public LogViewerControlViewModel(ILogDataStore dataStore)
    {
        DataStore = dataStore;
    }

    #endregion

    #region Properties

    public ILogDataStore DataStore { get; set; }

    #endregion
}
