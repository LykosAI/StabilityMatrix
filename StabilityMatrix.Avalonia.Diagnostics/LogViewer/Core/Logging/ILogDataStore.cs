using System.Collections.ObjectModel;

namespace StabilityMatrix.Avalonia.Diagnostics.LogViewer.Core.Logging;

public interface ILogDataStore
{
    ObservableCollection<LogModel> Entries { get; }
    void AddEntry(LogModel logModel);
}
