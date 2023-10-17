using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Xaml.Interactions.Draggable;
using Avalonia.Xaml.Interactivity;
using StabilityMatrix.Avalonia.ViewModels.Inference;

namespace StabilityMatrix.Avalonia.Controls;

public class StackEditableCard : TemplatedControl
{
    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        var listBox = e.NameScope.Find<ListBox>("PART_ListBox");
        if (listBox != null)
        {
            listBox.ContainerIndexChanged += (sender, args) =>
            {
                if (args.Container.DataContext is StackExpanderViewModel vm)
                {
                    vm.OnContainerIndexChanged(args.NewIndex);
                }
            };
        }
    }
}
