using Avalonia.Threading;

namespace StabilityMatrix.Avalonia.Diagnostics.LogViewer.Logging;

public class LogDataStore : Core.Logging.LogDataStore
{
    #region Methods

    public override async void AddEntry(Core.Logging.LogModel logModel) =>
        await Dispatcher.UIThread.InvokeAsync(() => base.AddEntry(logModel));

    #endregion
}
