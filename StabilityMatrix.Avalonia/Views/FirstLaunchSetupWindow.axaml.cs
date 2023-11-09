using System.Diagnostics.CodeAnalysis;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Views;

[Singleton]
public partial class FirstLaunchSetupWindow : AppWindowBase
{
    public ContentDialogResult Result { get; private set; }

    public FirstLaunchSetupWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    private void ContinueButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Result = ContentDialogResult.Primary;
        Close();
    }

    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    private void QuitButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Result = ContentDialogResult.None;
        Close();
    }
}
