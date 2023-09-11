using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using StabilityMatrix.Avalonia.Diagnostics.LogViewer.Core.Logging;

namespace StabilityMatrix.Avalonia.Diagnostics.LogViewer.Controls;

public partial class LogViewerControl : UserControl
{
    public LogViewerControl() => InitializeComponent();

    private ILogDataStoreImpl? vm;
    private LogModel? item;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is null)
            return;

        vm = (ILogDataStoreImpl)DataContext;
        vm.DataStore.Entries.CollectionChanged += OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            item = MyDataGrid.ItemsSource.Cast<LogModel>().LastOrDefault();
        });
    }

    protected void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (CanAutoScroll.IsChecked != true || item is null)
            return;

        MyDataGrid.ScrollIntoView(item, null);
        item = null;
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);

        if (vm is null)
            return;
        vm.DataStore.Entries.CollectionChanged -= OnCollectionChanged;
    }
}
