using System.ComponentModel;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using OpenIddict.Client;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Models.Api.Lykos;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Services;

[Localizable(false)]
[RegisterSingleton<IConnectedServiceManager>]
public class ConnectedServiceManager(
    IAccountsService accountsService,
    ISettingsManager settingsManager,
    ServiceManager<ViewModelBase> vmFactory,
    ILogger<ConnectedServiceManager> logger
) : IConnectedServiceManager
{
    /// <summary>
    /// Attempt to enable CivitUseDiscoveryApi, prompting for login as needed.
    /// </summary>
    public async Task<bool> PromptEnableCivitUseDiscoveryApi()
    {
        // Ensure logged in
        if (!await PromptLoginIfRequired() || accountsService.LykosStatus is not { User: { } user } status)
        {
            return false;
        }

        // Check if we can enable
        var canEnable = user.Permissions.Contains("read:discovery");
        if (!canEnable)
        {
            logger.LogDebug("User {Id} does not have permissions: {Permissions}", user.Id, user.Permissions);

            // Sponsor prompt
            var sponsorVm = vmFactory.Get<SponsorshipPromptViewModel>();
            sponsorVm.Initialize(status, "Accelerated Model Discovery", "Insider");
            await sponsorVm.ShowDialogAsync();

            return false;
        }

        logger.LogInformation("Enabling CivitUseDiscoveryApi");

        // Save settings
        settingsManager.Transaction(s => s.CivitUseDiscoveryApi, true);
        return true;
    }

    /// <summary>
    /// Prompts the user to log in to their Lykos account if they are not already logged in.
    /// </summary>
    /// <returns>True if the user is logged in after this function, false otherwise.</returns>
    public async Task<bool> PromptLoginIfRequired()
    {
        Exception? refreshException = null;

        // Check already logged in
        if (accountsService.LykosStatus?.IsConnected == true)
        {
            return true;
        }

        var hasStoredAccount = await accountsService.HasStoredLykosAccountAsync();

        if (hasStoredAccount)
        {
            // Try refresh
            try
            {
                await accountsService.RefreshLykosAsync();
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Error refreshing Lykos account");
                refreshException = e;
            }

            // Check again
            if (accountsService.LykosStatus?.IsConnected == true)
            {
                return true;
            }
        }

        // If we have a stored account but refresh failed
        var isSessionExpired = hasStoredAccount && refreshException is not null;

        var dialog = DialogHelper.CreateTaskDialog(
            isSessionExpired ? Resources.Text_Login_ExpiredTitle : Resources.Text_Login_ConnectTitle,
            isSessionExpired
                ? Resources.Text_Login_ExpiredDescription
                : Resources.Text_Login_ConnectDescription
        );

        dialog.Buttons =
        [
            new TaskDialogButton(Resources.Action_Login, TaskDialogStandardResult.OK) { IsDefault = true },
            new TaskDialogButton(Resources.Action_Close, TaskDialogStandardResult.Close),
        ];

        if (await dialog.ShowAsync(true) is not TaskDialogStandardResult.OK)
            return false;

        var vm = vmFactory.Get<OAuthDeviceAuthViewModel>();
        vm.ChallengeRequest = new OpenIddictClientModels.DeviceChallengeRequest
        {
            ProviderName = OpenIdClientConstants.LykosAccount.ProviderName
        };
        await vm.ShowDialogAsync();

        if (vm.AuthenticationResult is not { } result)
            return false;

        await accountsService.LykosAccountV2LoginAsync(
            new LykosAccountV2Tokens(result.AccessToken, result.RefreshToken, result.IdentityToken)
        );

        return true;
    }
}
