using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Languages;

namespace StabilityMatrix.Avalonia.ViewModels.Base;

/// <summary>
/// Base class for view models that are used in <see cref="FluentAvalonia.UI.Controls.TaskDialog"/>
/// </summary>
public abstract class TaskDialogViewModelBase : ViewModelBase
{
    private TaskDialog? dialog;

    public virtual string? Title { get; set; }

    protected static TaskDialogCommand GetCommandButton(string text, ICommand command)
    {
        return new TaskDialogCommand
        {
            Text = text,
            DialogResult = TaskDialogStandardResult.None,
            Command = command,
            IsDefault = true,
            ClosesOnInvoked = false
        };
    }

    protected static TaskDialogButton GetCloseButton()
    {
        return new TaskDialogButton
        {
            Text = Resources.Action_Close,
            DialogResult = TaskDialogStandardResult.Close
        };
    }

    protected static TaskDialogButton GetCloseButton(string text)
    {
        return new TaskDialogButton { Text = text, DialogResult = TaskDialogStandardResult.Close };
    }

    /// <summary>
    /// Return a <see cref="TaskDialog"/> that uses this view model as its content
    /// </summary>
    public virtual TaskDialog GetDialog()
    {
        Dispatcher.UIThread.VerifyAccess();

        dialog = new TaskDialog
        {
            Header = Title,
            Content = this,
            XamlRoot = App.VisualRoot,
            Buttons = { GetCloseButton() }
        };

        dialog.AttachedToVisualTree += (s, _) =>
        {
            ((TaskDialog)s!).Closing += OnDialogClosing;
        };
        dialog.DetachedFromVisualTree += (s, _) =>
        {
            ((TaskDialog)s!).Closing -= OnDialogClosing;
        };

        return dialog;
    }

    /// <summary>
    /// Show the dialog from <see cref="GetDialog"/> and return the result
    /// </summary>
    public async Task<TaskDialogStandardResult> ShowDialogAsync()
    {
        return (TaskDialogStandardResult)await GetDialog().ShowAsync(true);
    }

    protected void CloseDialog(TaskDialogStandardResult result)
    {
        dialog?.Hide(result);
    }

    protected virtual async void OnDialogClosing(object? sender, TaskDialogClosingEventArgs e) { }
}
