using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using Refit;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Validators;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(LykosLoginDialog))]
[RegisterTransient<LykosLoginViewModel>, ManagedService]
public partial class LykosLoginViewModel(
    IAccountsService accountsService,
    ServiceManager<ViewModelBase> vmFactory
) : TaskDialogViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueButtonClickCommand))]
    private bool isSignupMode;

    [ObservableProperty]
    [NotifyDataErrorInfo, NotifyCanExecuteChangedFor(nameof(ContinueButtonClickCommand))]
    [EmailAddress(ErrorMessage = "Email is not valid")]
    private string? email;

    [ObservableProperty]
    [NotifyDataErrorInfo, NotifyCanExecuteChangedFor(nameof(ContinueButtonClickCommand))]
    [Required]
    private string? username;

    [ObservableProperty]
    [NotifyDataErrorInfo, NotifyCanExecuteChangedFor(nameof(ContinueButtonClickCommand))]
    [Required]
    private string? password;

    [ObservableProperty]
    [NotifyDataErrorInfo, NotifyCanExecuteChangedFor(nameof(ContinueButtonClickCommand))]
    [Required, RequiresMatch<string>(nameof(Password))]
    private string? confirmPassword;

    [ObservableProperty]
    private AppException? loginError;

    [ObservableProperty]
    private AppException? signupError;

    public string SignupFooterMarkdown { get; } =
        """
                                                  By signing up, you are creating a
                                                  [lykos.ai](https://lykos.ai) Account and agree to our
                                                  [Terms](https://lykos.ai/terms-and-conditions) and
                                                  [Privacy Policy](https://lykos.ai/privacy)
                                                  """;

    private bool CanExecuteContinueButtonClick()
    {
        return !HasErrors && IsValid();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteContinueButtonClick))]
    private Task OnContinueButtonClick()
    {
        return IsSignupMode ? SignupAsync() : LoginAsync();
    }

    private async Task LoginAsync()
    {
        try
        {
            await accountsService.LykosLoginAsync(Email!, Password!);

            CloseDialog(TaskDialogStandardResult.OK);
        }
        catch (OperationCanceledException)
        {
            LoginError = new AppException("Request timed out", "Please try again later");
        }
        catch (ApiException e)
        {
            LoginError = e.StatusCode switch
            {
                HttpStatusCode.Unauthorized
                    => new AppException(
                        "Incorrect email or password",
                        "Please try again or reset your password"
                    ),
                _ => new AppException("Failed to login", $"{e.StatusCode} - {e.Message}")
            };
        }
    }

    private async Task SignupAsync()
    {
        try
        {
            await accountsService.LykosSignupAsync(Email!, Password!, Username!);

            CloseDialog(TaskDialogStandardResult.OK);
        }
        catch (OperationCanceledException)
        {
            SignupError = new AppException("Request timed out", "Please try again later");
        }
        catch (ApiException e)
        {
            SignupError = new AppException("Failed to signup", $"{e.StatusCode} - {e.Message}");
        }
    }

    [RelayCommand]
    private async Task OnGoogleOAuthButtonClick()
    {
        var vm = vmFactory.Get<OAuthGoogleLoginViewModel>();

        if (await vm.GetDialog().ShowAsync() is ContentDialogResult.Primary)
        {
            CloseDialog(TaskDialogStandardResult.OK);
        }
    }

    /// <inheritdoc />
    public override TaskDialog GetDialog()
    {
        var dialog = base.GetDialog();
        dialog.Buttons = new List<TaskDialogButton>
        {
            GetCommandButton(Resources.Action_Continue, ContinueButtonClickCommand),
            GetCloseButton()
        };
        return dialog;
    }

    private bool IsValid()
    {
        if (IsSignupMode)
        {
            return !(
                string.IsNullOrEmpty(Email)
                || string.IsNullOrEmpty(Username)
                || string.IsNullOrEmpty(Password)
                || string.IsNullOrEmpty(ConfirmPassword)
            );
        }

        return !(string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password));
    }
}
