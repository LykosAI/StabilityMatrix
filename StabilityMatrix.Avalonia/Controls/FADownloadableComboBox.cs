using System;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Controls;

// ReSharper disable once InconsistentNaming
public partial class FADownloadableComboBox : FAComboBox
{
    protected override Type StyleKeyOverride => typeof(FADownloadableComboBox);

    static FADownloadableComboBox()
    {
        SelectionChangedEvent.AddClassHandler<FADownloadableComboBox>(
            (comboBox, args) => comboBox.OnSelectionChanged(args)
        );
    }

    protected virtual void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        // On downloadable added
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is IDownloadableResource { IsDownloadable: true } item)
        {
            // Reset the selection
            e.Handled = true;

            if (
                e.RemovedItems.Count > 0
                && e.RemovedItems[0] is IDownloadableResource { IsDownloadable: false } removedItem
            )
            {
                SelectedItem = removedItem;
            }
            else
            {
                SelectedItem = null;
            }

            // Show dialog to download the model
            PromptDownloadCommand.ExecuteAsync(item).SafeFireAndForget();
        }
    }

    [RelayCommand]
    private static async Task PromptDownloadAsync(IDownloadableResource downloadable)
    {
        if (downloadable.DownloadableResource is not { } resource)
            return;

        var vmFactory = App.Services.GetRequiredService<ServiceManager<ViewModelBase>>();
        var confirmDialog = vmFactory.Get<DownloadResourceViewModel>();
        confirmDialog.Resource = resource;
        confirmDialog.FileName = resource.FileName;

        if (await confirmDialog.GetDialog().ShowAsync() == ContentDialogResult.Primary)
        {
            confirmDialog.StartDownload();
        }
    }
}
