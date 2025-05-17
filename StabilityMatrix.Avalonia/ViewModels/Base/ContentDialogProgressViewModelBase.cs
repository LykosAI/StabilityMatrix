using System;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;

namespace StabilityMatrix.Avalonia.ViewModels.Base;

public partial class ContentDialogProgressViewModelBase : ConsoleProgressViewModel
{
    [ObservableProperty]
    private bool hideCloseButton;

    [ObservableProperty]
    private bool autoScrollToBottom = true;

    public event EventHandler<ContentDialogResult>? PrimaryButtonClick;
    public event EventHandler<ContentDialogResult>? SecondaryButtonClick;
    public event EventHandler<ContentDialogResult>? CloseButtonClick;

    public virtual void OnPrimaryButtonClick()
    {
        PrimaryButtonClick?.Invoke(this, ContentDialogResult.Primary);
    }

    public virtual void OnSecondaryButtonClick()
    {
        SecondaryButtonClick?.Invoke(this, ContentDialogResult.Secondary);
    }

    public virtual void OnCloseButtonClick()
    {
        CloseButtonClick?.Invoke(this, ContentDialogResult.None);
    }
}
