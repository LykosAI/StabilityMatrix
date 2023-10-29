using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using Size = System.Drawing.Size;

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

    [MethodImpl(MethodImplOptions.Synchronized)]
    private void LoadUserImage(ImageSource image)
    {
        var current = ImageSource;

        ImageSource = image;

        current?.Dispose();

        // Cache the hash for later upload use
        image.GetBlake3HashAsync().SafeFireAndForget();
    }

    [RelayCommand]
    private async Task SelectImageFromFilePickerAsync()
    {
        var files = await App.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new("Png") { Patterns = new[] { "*.png" } },
                    new("Jpg") { Patterns = new[] { "*.jpg", "*.jpeg" } }
                }
            }
        );

        if (files.Count == 0)
            return;

        var image = new ImageSource(files[0].TryGetLocalPath()!);

        LoadUserImage(image);
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

                Dispatcher.UIThread.Post(
                    () => LoadUserImage(new ImageSource(imageFile.AbsolutePath))
                );

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

                    Dispatcher.UIThread.Post(() => LoadUserImage(new ImageSource(path)));
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
