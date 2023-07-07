using System;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Avalonia.ViewModels;

/// <summary>
///  This is our ViewModel for the second page
/// </summary>
partial class SecondPageViewModel : PageViewModelBase
{
    public SecondPageViewModel()
    {
    }

    [Required]
    [EmailAddress]
    [ObservableProperty]
    private string? mailAddress;

    [Required]
    [ObservableProperty]
    private string? password;

    public override bool CanNavigateNext { get; protected set; } = true;
    public override bool CanNavigatePrevious { get; protected set; }
}
