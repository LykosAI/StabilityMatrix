using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels;

namespace StabilityMatrix.Avalonia.Views;

public partial class InferencePage : UserControlBase
{
    private Button? _addButton;
    private Button AddButton => _addButton 
        ??= this.FindControl<TabView>("TabView")!
            .GetTemplateChildren()
            .OfType<Button>()
            .First(p => p.Name == "AddButton");
    
    private readonly CommandBarFlyout addTabFlyout;
    
    public InferencePage()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, DropHandler);
        AddHandler(DragDrop.DragOverEvent, DragOverHandler);
        
        addTabFlyout = Resources["AddTabFlyout"] as CommandBarFlyout 
                       ?? throw new NullReferenceException("AddTabFlyout not found");
    }

    private void TabView_OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        (DataContext as InferenceViewModel)?.OnTabCloseRequested(args);
    }

    private void DragOverHandler(object? sender, DragEventArgs e)
    {
        if (DataContext is IDropTarget dropTarget)
        {
            dropTarget.DragOver(sender, e);
        }
    }

    private void DropHandler(object? sender, DragEventArgs e)
    {
        if (DataContext is IDropTarget dropTarget)
        {
            dropTarget.Drop(sender, e);
        }
    }

    private void TabView_OnAddTabButtonClick(TabView sender, EventArgs args)
    {
        ShowAddTabMenu(false);
    }
    
    private void ShowAddTabMenu(bool isTransient)
    {
        addTabFlyout.ShowMode = isTransient ? FlyoutShowMode.Transient : FlyoutShowMode.Standard;
        
        addTabFlyout.ShowAt(AddButton);
    }

    private void AddTabMenu_TextToImageButton_OnClick(object? sender, RoutedEventArgs e)
    {
        (DataContext as InferenceViewModel)!.AddTabCommand.Execute(null);
    }
}
