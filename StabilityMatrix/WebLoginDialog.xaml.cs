using System.Windows;
using Microsoft.Web.WebView2.Core;
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

    private readonly Microsoft.Web.WebView2.Wpf.WebView2 currentWebView;
    
    public WebLoginViewModel ViewModel { get; set; }
    
    public WebLoginDialog(IContentDialogService dialogService, WebLoginViewModel viewModel) : base(
        dialogService.GetContentPresenter())
    {
        InitializeComponent();
        DataContext = viewModel;
        ViewModel = viewModel;
        currentWebView = LoginWebView;
    }

    // Pass through OnLoaded to ViewModel
    private void WebLoginDialog_OnLoaded(object sender, RoutedEventArgs e) => ViewModel.OnLoaded();

    // On nav complete we run js to hide scrollbar while allowing scrolling
    private async void LoginWebView_OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            await currentWebView.ExecuteScriptAsync(DisableScrollbarJs);
        }
        // Pass to ViewModel event
        ViewModel.OnNavigationCompleted(currentWebView.Source);
    }
    
    // This happens before OnNavigationCompleted
    private async void LoginWebView_OnSourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
    {
        var url = currentWebView.Source;
        var cookies = url is null ? null :
            await currentWebView.CoreWebView2.CookieManager.GetCookiesAsync(url.AbsoluteUri);
        ViewModel.OnSourceChanged(currentWebView.Source, cookies);
    }
}
