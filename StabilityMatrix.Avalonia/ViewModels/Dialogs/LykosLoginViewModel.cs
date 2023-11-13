using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Validators;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(LykosLoginDialog))]
[Transient, ManagedService]
public partial class LykosLoginViewModel : TaskDialogViewModelBase
{
    private readonly IAccountsService accountsService;

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

    public LykosLoginViewModel(IAccountsService accountsService)
    {
        this.accountsService = accountsService;
    }

    private bool CanExecuteContinueButtonClick()
    {
        return !HasErrors && IsValid();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteContinueButtonClick))]
    private async Task OnContinueButtonClick()
    {
        try
        {
            await accountsService.LykosLoginAsync(Email!, Password!);

            CloseDialog(TaskDialogStandardResult.OK);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
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
