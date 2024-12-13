using System.Linq;
using Avalonia.Controls;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels.Settings;
using StabilityMatrix.Core.Models.Update;

namespace StabilityMatrix.Avalonia.Views.Settings;

[RegisterSingleton<UpdateSettingsPage>]
public partial class UpdateSettingsPage : UserControlBase
{
    public UpdateSettingsPage()
    {
        InitializeComponent();
    }

    private void ChannelListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var listBox = (ListBox)sender!;

        if (e.AddedItems.Count == 0 || e.AddedItems[0] is not UpdateChannelCard item)
        {
            return;
        }

        var vm = (UpdateSettingsViewModel)DataContext!;

        if (!vm.VerifyChannelSelection(item))
        {
            listBox.Selection.Clear();

            listBox.Selection.SelectedItem = vm.AvailableUpdateChannelCards.First(
                c => c.UpdateChannel == UpdateChannel.Stable
            );

            vm.ShowLoginRequiredDialog();
        }
    }
}
