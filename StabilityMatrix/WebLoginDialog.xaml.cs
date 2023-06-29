using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using CefSharp;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls.ContentDialogControl;

namespace StabilityMatrix;

public partial class WebLoginDialog : ContentDialog
{
    private const string DisableScrollbarJs =
        @"document.querySelector('body').style.overflow='scroll';
        var style=document.createElement('style');style.type='text/css';
        style.innerHTML='::-webkit-scrollbar{display:none}';
        document.getElementsByTagName('body')[0].appendChild(style)";
    
    public WebLoginDialog(IContentDialogService dialogService, WebLoginViewModel viewModel) : base(
        dialogService.GetContentPresenter())
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void WebLoginDialog_OnLoaded(object sender, RoutedEventArgs e)
    {
        ((WebLoginViewModel) DataContext).OnLoaded();
    }

    private void Browser_OnFrameLoadEnd(object? sender, FrameLoadEndEventArgs e)
    {
        if (e.Frame.IsMain)
        {
            e.Browser.MainFrame.ExecuteJavaScriptAsync(DisableScrollbarJs);
        }
    }

    private async void Browser_OnAddressChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is not WebLoginViewModel dataContext) return;
        await dataContext.OnRefresh();
    }
}
