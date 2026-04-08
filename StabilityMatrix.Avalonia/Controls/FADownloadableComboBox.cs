using System;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Controls;

// ReSharper disable once InconsistentNaming
public partial class FADownloadableComboBox : FAComboBox
{
    protected override Type StyleKeyOverride => typeof(FAComboBox);

    private Popup? dropDownPopup;
    private IDisposable? openSubscription;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        CleanupSubscription();
        DropDownOpened -= OnDropDownOpenedHandler;
        DropDownClosed -= OnDropDownClosedHandler;

        // Template part name is "Popup" per FAComboBox.properties.cs (s_tpPopup = "Popup")
        dropDownPopup = e.NameScope.Find<Popup>("Popup");

        DropDownOpened += OnDropDownOpenedHandler;
        DropDownClosed += OnDropDownClosedHandler;
    }

    private void OnDropDownOpenedHandler(object? sender, EventArgs e)
    {
        CleanupSubscription();

        if (dropDownPopup?.Child is not Control popupChild)
            return;

        var scrollViewer = popupChild.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();

        if (scrollViewer == null)
            return;

        // On Unix-like systems, overlay popups share the same TopLevel visual root as the main window.
        // FAComboBox.OnPopupOpened adds a TopLevel tunnel handler that marks all wheel eventsas handled while the dropdown is open,
        // which inadvertently blocks scroll-wheelevents in popup menus in Inference model cards.
        // Resetting e.Handled on the ScrollViewer's tunnel phase counters this.
        if (!Compat.IsUnix)
            return;

        openSubscription = scrollViewer.AddDisposableHandler(
            PointerWheelChangedEvent,
            static (_, ev) =>
            {
                if (ev.Handled)
                    ev.Handled = false;
            },
            RoutingStrategies.Tunnel,
            handledEventsToo: true
        );
    }

    private void OnDropDownClosedHandler(object? sender, EventArgs e)
    {
        CleanupSubscription();
    }

    private void CleanupSubscription()
    {
        openSubscription?.Dispose();
        openSubscription = null;
    }

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
