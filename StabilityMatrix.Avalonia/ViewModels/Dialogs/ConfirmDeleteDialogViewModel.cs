using Avalonia.Controls.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Native;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(ConfirmDeleteDialog))]
[RegisterTransient<ConfirmDeleteDialogViewModel>]
[ManagedService]
public partial class ConfirmDeleteDialogViewModel(ILogger<ConfirmDeleteDialogViewModel> logger)
    : ContentDialogViewModelBase
{
    [ObservableProperty]
    private string title = "Confirm Delete";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfirmDeleteButtonText))]
    [NotifyPropertyChangedFor(nameof(IsPermanentDelete))]
    [NotifyPropertyChangedFor(nameof(DeleteFollowingFilesText))]
    private bool isRecycleBinAvailable = NativeFileOperations.IsRecycleBinAvailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfirmDeleteButtonText))]
    [NotifyPropertyChangedFor(nameof(IsPermanentDelete))]
    [NotifyPropertyChangedFor(nameof(DeleteFollowingFilesText))]
    private bool isRecycleBinOptOutChecked;

    public bool IsPermanentDelete => !IsRecycleBinAvailable || IsRecycleBinOptOutChecked;

    public string ConfirmDeleteButtonText =>
        IsPermanentDelete ? Resources.Action_Delete : Resources.Action_MoveToTrash;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeleteFollowingFilesText))]
    private IReadOnlyList<string> pathsToDelete = [];

    public string DeleteFollowingFilesText =>
        PathsToDelete.Count is var count and > 1
            ? string.Format(Resources.TextTemplate_DeleteFollowingCountItems, count)
            : Resources.Text_DeleteFollowingItems;

    public bool ShowActionCannotBeUndoneNotice { get; set; } = true;

    /// <inheritdoc />
    public override BetterContentDialog GetDialog()
    {
        var dialog = base.GetDialog();

        dialog.MinDialogWidth = 550;
        dialog.MaxDialogHeight = 600;
        dialog.IsFooterVisible = false;
        dialog.CloseOnClickOutside = true;
        dialog.ContentVerticalScrollBarVisibility = ScrollBarVisibility.Disabled;

        return dialog;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteConfirmDelete))]
    private void OnConfirmDeleteClick()
    {
        OnPrimaryButtonClick();
    }

    private bool CanExecuteConfirmDelete()
    {
        return !HasErrors && IsValid();
    }

    private bool IsValid()
    {
        return true;
    }

    public async Task ExecuteCurrentDeleteOperationAsync(bool ignoreErrors = false, bool failFast = false)
    {
        var paths = PathsToDelete;

        var exceptions = new List<Exception>();

        if (!IsPermanentDelete)
        {
            // Recycle bin
            if (!NativeFileOperations.IsRecycleBinAvailable)
            {
                throw new NotSupportedException("Recycle bin is not available on this platform");
            }

            try
            {
                await NativeFileOperations.RecycleBin.MoveFilesToRecycleBinAsync(paths);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Failed to move path to recycle bin");

                if (!ignoreErrors)
                {
                    exceptions.Add(e);

                    if (failFast)
                    {
                        throw new AggregateException(exceptions);
                    }
                }
            }
        }
        else
        {
            await Task.Run(() =>
            {
                foreach (var path in paths)
                {
                    try
                    {
                        if (Directory.Exists(path))
                        {
                            Directory.Delete(path, true);
                        }
                        else
                        {
                            File.Delete(path);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning(e, "Failed to delete path");

                        if (!ignoreErrors)
                        {
                            exceptions.Add(e);

                            if (failFast)
                            {
                                throw new AggregateException(exceptions);
                            }
                        }
                    }
                }
            });
        }
    }
}
