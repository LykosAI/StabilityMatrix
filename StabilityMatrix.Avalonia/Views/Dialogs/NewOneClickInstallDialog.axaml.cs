using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using KGySoft.CoreLibraries;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Core.Models.Packages;
using ItemsRepeater = Avalonia.Controls.ItemsRepeater;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

public partial class NewOneClickInstallDialog : UserControlBase
{
    public NewOneClickInstallDialog()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        var teachingTip =
            this.FindControl<TeachingTip>("InferenceTeachingTip")
            ?? throw new InvalidOperationException("TeachingTip not found");

        teachingTip.ActionButtonClick += (_, _) =>
        {
            teachingTip.IsOpen = false;
        };

        // Find ComfyUI listbox item
        var listBox = this.FindControl<ItemsRepeater>("PackagesRepeater");

        // Find ComfyUI listbox item
        if (listBox?.ItemsSource?.Cast<BasePackage>().FirstOrDefault(p => p is ComfyUI) is { } comfy)
        {
            var comfyItem = listBox.TryGetElement(listBox?.ItemsSource?.IndexOf(comfy) ?? 0);

            // comfyItem!.IsSelected = true;

            teachingTip.Target = comfyItem;
            teachingTip.IsOpen = true;
            teachingTip.CloseButtonCommand = null;
        }
    }
}
