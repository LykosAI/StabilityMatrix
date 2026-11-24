using System;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Controls;

public partial class BetterDownloadableComboBox : BetterComboBox
{
    protected override Type StyleKeyOverride => typeof(BetterComboBox);

    static BetterDownloadableComboBox()
    {
        SelectionChangedEvent.AddClassHandler<BetterDownloadableComboBox>(
            (comboBox, args) => comboBox.OnSelectionChanged(args)
        );
    }

    protected virtual void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        // On downloadable added
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is IDownloadableResource { IsDownloadable: true } item)
        {
            e.Handled = true;

            var restoreItem =
                e.RemovedItems.Count > 0
                && e.RemovedItems[0] is IDownloadableResource { IsDownloadable: false } removedItem
                    ? (object)removedItem
                    : null;

            // Reset the selection AFTER the control finishes committing it. Assigning
            // SelectedItem synchronously inside the SelectionChanged handler races that
            // commit; when the control wins, the downloadable stays "selected" and
            // re-clicking it raises no SelectionChanged at all — the historical
            // "clicking a cloud entry sometimes does nothing" flakiness.
            Dispatcher.UIThread.Post(() => SelectedItem = restoreItem);

            // Show dialog to download the model
            PromptDownloadCommand.ExecuteAsync(item).SafeFireAndForget();
        }
    }

    [RelayCommand]
    private static async Task PromptDownloadAsync(IDownloadableResource downloadable)
    {
        if (downloadable.DownloadableResource is not { } resource)
            return;

        var vmFactory = App.Services.GetRequiredService<IServiceManager<ViewModelBase>>();
        var confirmDialog = vmFactory.Get<DownloadResourceViewModel>();
        confirmDialog.Resource = resource;
        confirmDialog.FileName = resource.FileName;

        if (await confirmDialog.GetDialog().ShowAsync() == ContentDialogResult.Primary)
        {
            confirmDialog.StartDownload();
        }
    }
}
