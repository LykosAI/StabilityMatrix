using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using FluentAvalonia.UI.Windowing;
using StabilityMatrix.Avalonia.ViewModels;

namespace StabilityMatrix.Avalonia.Views;

public partial class MainWindow : AppWindow
{
    public MainWindow()
    {
        InitializeComponent();
        
#if DEBUG
        this.AttachDevTools();
#endif

        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;
    }
}
