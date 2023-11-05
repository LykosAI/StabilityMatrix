using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Core.Models.Packages;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

public partial class OneClickInstallDialog : UserControl
{
    public OneClickInstallDialog()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
    }

    /// <inheritdoc />
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
        var listBox = this.FindControl<ListBox>("PackagesListBox");

        // Find ComfyUI listbox item
        if (listBox?.Items.Cast<BasePackage>().FirstOrDefault(p => p is ComfyUI) is { } comfy)
        {
            var comfyItem = listBox.ContainerFromItem(comfy) as ListBoxItem;

            // comfyItem!.IsSelected = true;

            teachingTip.Target = comfyItem;
            teachingTip.IsOpen = true;
        }
    }
}
