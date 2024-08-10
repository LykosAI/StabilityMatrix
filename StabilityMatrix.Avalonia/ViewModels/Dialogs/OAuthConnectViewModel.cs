using System;
using System.Threading.Tasks;
using System.Web;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using MessagePipe;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
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
                logger.LogDebug("UriHandler Received URI: {Uri}", receivedUri.ToString());

                // Ignore if path not matching
                if (
                    !receivedUri.PathAndQuery.StartsWith(
                        "/oauth/patreon/callback",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return;
                }

                var queryCollection = HttpUtility.ParseQueryString(receivedUri.Query);
                var status = queryCollection.Get("status");
                var error = queryCollection.Get("error");

                if (status == "success")
                {
                    logger.LogInformation("OAuth connection successful");
                    OnPrimaryButtonClick();
                }
                else if (status == "failure")
                {
                    logger.LogError("OAuth connection failed ({Status}): {Error}", status, error);

                    var dialog = DialogHelper.CreateMarkdownDialog(
                        $"- {error}",
                        Resources.Label_ConnectAccountFailed
                    );

                    dialog.ShowAsync().ContinueWith(_ => OnCloseButtonClick()).SafeFireAndForget();
                }
                else
                {
                    logger.LogError("OAuth connection unknown status ({Status}): {Error}", status, error);

                    var dialog = DialogHelper.CreateMarkdownDialog(
                        $"- {error}",
                        Resources.Label_ConnectAccountFailed
                    );

                    dialog.ShowAsync().ContinueWith(_ => OnCloseButtonClick()).SafeFireAndForget();
                }
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

    public override BetterContentDialog GetDialog()
    {
        return new BetterContentDialog
        {
            Title = Title,
            Content = this,
            CloseButtonText = Resources.Action_Close
        };
    }
}
