using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(LaunchPageView))]
public class LaunchPageViewModel : PageViewModelBase
{
    /// <summary>
    /// The Title of this page
    /// </summary>
    public override string Title => "Launch";

    public override Symbol Icon => Symbol.PlayFilled;

    /// <summary>
    /// The content of this page
    /// </summary>
    public string Message => "Press \"Next\" to register yourself.";

    public override bool CanNavigateNext { get; protected set; }
    public override bool CanNavigatePrevious { get; protected set; }
}
