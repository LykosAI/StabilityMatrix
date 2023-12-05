using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Database;
using Size = System.Drawing.Size;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(SelectImageCard))]
[ManagedService]
[Transient]
public partial class SelectImageCardViewModel(INotificationService notificationService)
    : ViewModelBase,
        IDropTarget,
        IComfyStep
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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
        CurrentBitmap is null
            ? null
            : new Size(Convert.ToInt32(CurrentBitmap.Size.Width), Convert.ToInt32(CurrentBitmap.Size.Height));

    /// <inheritdoc />
    public void ApplyStep(ModuleApplyStepEventArgs e)
    {
        e.Builder.SetupImagePrimarySource(
            ImageSource ?? throw new ValidationException("Input Image is required"),
            CurrentBitmapSize ?? throw new ValidationException("Input Image is required"),
            e.Builder.Connections.BatchIndex
        );
    }

    [RelayCommand]
    private async Task SelectImageFromFilePickerAsync()
    {
        var files = await App.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions { FileTypeFilter = [FilePickerFileTypes.ImagePng, FilePickerFileTypes.ImageJpg] }
        );

        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
        {
            LoadUserImageSafe(new ImageSource(path));
        }
    }

    /// <summary>
    /// Supports LocalImageFile Context or OS Files
    /// </summary>
    public void DragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.GetDataFormats().Contains(DataFormats.Files) || e.Data.GetContext<LocalImageFile>() is not null)
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
        if (e.Data.GetContext<LocalImageFile>() is { } imageFile)
        {
            e.Handled = true;

            Dispatcher.UIThread.Post(() => LoadUserImageSafe(new ImageSource(imageFile.AbsolutePath)));

            return;
        }
        // 2. OS Files
        if (e.Data.GetFiles() is { } files && files.Select(f => f.TryGetLocalPath()).FirstOrDefault() is { } path)
        {
            e.Handled = true;

            Dispatcher.UIThread.Post(() => LoadUserImageSafe(new ImageSource(path)));
        }
    }

    /// <summary>
    /// Calls <see cref="LoadUserImage"/> with notification error handling.
    /// </summary>
    private void LoadUserImageSafe(ImageSource image)
    {
        try
        {
            LoadUserImage(image);
        }
        catch (Exception e)
        {
            Logger.Warn(e, "Error loading image");
            notificationService.ShowPersistent("Error loading image", e.Message);
        }
    }

    /// <summary>
    /// Loads the user image from the given ImageSource.
    /// </summary>
    /// <param name="image">The ImageSource object representing the user image.</param>
    [MethodImpl(MethodImplOptions.Synchronized)]
    private void LoadUserImage(ImageSource image)
    {
        var current = ImageSource;

        ImageSource = image;

        current?.Dispose();

        // Cache the hash for later upload use
        image.GetBlake3HashAsync().SafeFireAndForget();
    }
}
