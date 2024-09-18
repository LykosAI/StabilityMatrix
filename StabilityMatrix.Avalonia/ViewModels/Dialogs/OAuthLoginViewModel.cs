using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using AsyncAwaitBestPractices;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MessagePipe;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

/// <summary>
/// Like <see cref="OAuthConnectViewModel"/>, but for handling full code responses from OAuth providers,
/// instead of being able to just close and refresh state.
/// </summary>
[Transient]
[ManagedService]
[View(typeof(OAuthLoginDialog))]
public partial class OAuthLoginViewModel(
    ILogger<OAuthConnectViewModel> logger,
    IDistributedSubscriber<string, Uri> uriHandlerSubscriber
) : ContentDialogViewModelBase, IAsyncDisposable
{
    private IAsyncDisposable? uriHandlerSubscription;

    /// <summary>
    /// Name of the service to connect to
    /// </summary>
    public virtual string ServiceName { get; set; } = "";

    /// <summary>
    /// Url to open in the browser
    /// </summary>
    [ObservableProperty]
    private string? url;

    public override string Title => string.Format(TitleTemplate, ServiceName).Trim();

    public virtual string TitleTemplate => Resources.TextTemplate_OAuthLoginTitle;

    public virtual string? Description { get; set; } = Resources.Text_OAuthLoginDescription;

    public virtual string? AppLinkInstructions { get; set; } = Resources.Text_AllowBrowserOpenAppLink;

    // ReSharper disable once LocalizableElement
    public virtual string CallbackUriPath { get; set; } = "/oauth/default/callback";

    [ObservableProperty]
    private bool isLoading = true;

    /// <inheritdoc />
    public override async Task OnLoadedAsync()
    {
        await base.OnLoadedAsync();

        uriHandlerSubscription = await uriHandlerSubscriber.SubscribeAsync(
            UriHandler.IpcKeySend,
            OnCallbackUriReceived
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

    protected virtual void OnLoginFailed(IEnumerable<(string Message, string? Detail)> errors)
    {
        // ReSharper disable twice LocalizableElement
        var content = string.Join(
            "\n",
            errors.Select(e => e.Detail is null ? $"- **{e.Message}**" : $"- **{e.Message}**: {e.Detail}")
        );

        Dispatcher.UIThread.Post(() =>
        {
            var dialog = DialogHelper.CreateMarkdownDialog(content, Resources.Label_ConnectAccountFailed);

            dialog.ShowAsync().ContinueWith(_ => OnCloseButtonClick()).SafeFireAndForget();
        });
    }

    protected virtual Task OnCallbackUriMatchedAsync(Uri uri) => Task.CompletedTask;

    private void OnCallbackUriReceived(Uri uri)
    {
        // Ignore if path not matching
        if (uri.AbsolutePath != CallbackUriPath)
        {
            logger.LogDebug("Received Callback URI: {Uri}", uri.RedactQueryValues());

            return;
        }

        logger.LogInformation("Matched Callback URI: {Uri}", uri.RedactQueryValues());

        OnCallbackUriMatchedAsync(uri).SafeFireAndForget();
    }

    /// <inheritdoc />
    public override BetterContentDialog GetDialog()
    {
        var dialog = base.GetDialog();

        dialog.CloseButtonText = Resources.Action_Cancel;

        return dialog;
    }

    public async ValueTask DisposeAsync()
    {
        if (uriHandlerSubscription is not null)
        {
            await uriHandlerSubscription.DisposeAsync();
            uriHandlerSubscription = null;
        }

        GC.SuppressFinalize(this);
    }
}
