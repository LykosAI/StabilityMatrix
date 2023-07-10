using Avalonia;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Windowing;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;

namespace StabilityMatrix.Avalonia.Views;

public partial class MainWindow : AppWindowBase
{
    public INotificationService? NotificationService { get; set; }
    
    public MainWindow()
    {
        InitializeComponent();
        
#if DEBUG
        this.AttachDevTools();
#endif
        
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;
    }

    public override void OnLoaded(object? sender, RoutedEventArgs e)
    {
        base.OnLoaded(sender, e);
        NotificationService?.Initialize(this);
    }
}
