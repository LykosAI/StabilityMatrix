using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MessagePipe;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(OAuthConnectDialog))]
[Transient, ManagedService]
public partial class OAuthConnectViewModel : ContentDialogViewModelBase
{
    private readonly ILogger<OAuthConnectViewModel> logger;
    private readonly IDistributedSubscriber<string, Uri> uriHandlerSubscriber;

    private IAsyncDisposable? uriHandlerSubscription;

    [ObservableProperty]
    private string? title = "Connect OAuth";

    [ObservableProperty]
    private string? url;

    [ObservableProperty]
    private string? description =
        "Please login and click 'Allow' in the opened browser window to connect with StabilityMatrix.\n\n"
        + "Once you have done so, close this prompt to complete the connection.";

    [ObservableProperty]
    private string? footer = "Once you have done so, close this prompt to complete the connection.";

    public OAuthConnectViewModel(
        ILogger<OAuthConnectViewModel> logger,
        IDistributedSubscriber<string, Uri> uriHandlerSubscriber
    )
    {
        this.logger = logger;
        this.uriHandlerSubscriber = uriHandlerSubscriber;
    }

    /// <inheritdoc />
    public override async Task OnLoadedAsync()
    {
        await base.OnLoadedAsync();

        uriHandlerSubscription = await uriHandlerSubscriber.SubscribeAsync(
            UriHandler.IpcKeySend,
            receivedUri =>
            {
                logger.LogDebug("UriHandler Received URI: {Uri}", receivedUri);
                OnPrimaryButtonClick();
            }
        );
    }

    /// <inheritdoc />
    public override async Task OnUnloadedAsync()
    {
        if (uriHandlerSubscription is not null)
        {
            await uriHandlerSubscription.DisposeAsync();
            uriHandlerSubscription = null;
        }
    }

    public BetterContentDialog GetDialog()
    {
        return new BetterContentDialog
        {
            Title = Title,
            Content = this,
            CloseButtonText = Resources.Action_Close
        };
    }
}
