using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using NLog;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(SelectImageCard))]
[ManagedService]
[Transient]
public partial class SelectImageCardViewModel : ViewModelBase, IDropTarget
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly INotificationService notificationService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelectionAvailable))]
    private ImageSource? imageSource;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentBitmapSize))]
    private IImage? currentBitmap;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelectionAvailable))]
    private bool isSelectionEnabled = true;

    public bool IsSelectionAvailable => IsSelectionEnabled && ImageSource == null;

    public Size? CurrentBitmapSize =>
        new Size(
            Convert.ToInt32(CurrentBitmap?.Size.Width ?? 0),
            Convert.ToInt32(CurrentBitmap?.Size.Height ?? 0)
        );

    public SelectImageCardViewModel(INotificationService notificationService)
    {
        this.notificationService = notificationService;
    }

    /// <inheritdoc />
    public void DragOver(object? sender, DragEventArgs e)
    {
        // 1. Context drop for LocalImageFile
        if (e.Data.GetDataFormats().Contains("Context"))
        {
            if (e.Data.Get("Context") is LocalImageFile imageFile)
            {
                e.Handled = true;
                return;
            }

            e.DragEffects = DragDropEffects.None;
        }
        // 2. OS Files
        if (e.Data.GetDataFormats().Contains(DataFormats.Files))
        {
            e.Handled = true;
            return;
        }

        e.DragEffects = DragDropEffects.None;
    }

    /// <inheritdoc />
    public void Drop(object? sender, DragEventArgs e)
    {
        // 1. Context drop for LocalImageFile
        if (e.Data.GetDataFormats().Contains("Context"))
        {
            if (e.Data.Get("Context") is LocalImageFile imageFile)
            {
                e.Handled = true;

                Dispatcher.UIThread.Post(() =>
                {
                    var current = ImageSource;

                    ImageSource = new ImageSource(imageFile.AbsolutePath);

                    current?.Dispose();
                });

                return;
            }
        }
        // 2. OS Files
        if (e.Data.GetDataFormats().Contains(DataFormats.Files))
        {
            e.Handled = true;

            try
            {
                if (e.Data.Get(DataFormats.Files) is IEnumerable<IStorageItem> files)
                {
                    var path = files.Select(f => f.Path.LocalPath).FirstOrDefault();

                    if (string.IsNullOrWhiteSpace(path))
                    {
                        return;
                    }

                    Dispatcher.UIThread.Post(() =>
                    {
                        var current = ImageSource;

                        ImageSource = new ImageSource(path);

                        current?.Dispose();
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load image from drop");
                notificationService.ShowPersistent("Failed to load source image", ex.Message);
            }
        }
    }
}
