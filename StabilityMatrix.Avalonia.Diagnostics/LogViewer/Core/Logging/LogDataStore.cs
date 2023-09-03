using System.Collections.ObjectModel;
using Avalonia.Threading;

namespace StabilityMatrix.Avalonia.Diagnostics.LogViewer.Core.Logging;

public class LogDataStore : ILogDataStore
{
    public static LogDataStore Instance { get; } = new();
    
    #region Fields

    private static readonly SemaphoreSlim _semaphore = new(initialCount: 1);

    #endregion

    #region Properties

    public ObservableCollection<LogModel> Entries { get; } = new();

    #endregion

    #region Methods

    public virtual void AddEntry(LogModel logModel)
    {
        // ensure only one operation at time from multiple threads
        _semaphore.Wait();

        Dispatcher.UIThread.Post(() =>
        {
            Entries.Add(logModel);
        });

        _semaphore.Release();
    }

    #endregion
}
