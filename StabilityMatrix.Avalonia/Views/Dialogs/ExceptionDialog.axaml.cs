using System.Diagnostics.CodeAnalysis;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Windowing;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<ExceptionDialog>]
public partial class ExceptionDialog : AppWindowBase
{
    public ExceptionDialog()
    {
        InitializeComponent();

        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    private async void CopyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var content = (DataContext as ExceptionViewModel)?.FormatAsMarkdown();

        if (content is not null && Clipboard is not null)
        {
            await Clipboard.SetTextAsync(content);
        }
    }

    private void ContinueButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ExceptionViewModel viewModel)
        {
            viewModel.IsContinueResult = true;
        }

        Close();
    }

    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    private void ExitButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
