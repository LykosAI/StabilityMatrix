using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Input;
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
#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(SelectImageCard))]
[ManagedService]
[Transient]
public partial class SelectImageCardViewModel(INotificationService notificationService)
    : LoadableViewModelBase,
        IDropTarget,
        IComfyStep,
        IInputImageProvider
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelectionAvailable))]
    [NotifyPropertyChangedFor(nameof(IsImageFileNotFound))]
    private ImageSource? imageSource;

    [ObservableProperty]
    [property: JsonIgnore]
    [NotifyPropertyChangedFor(nameof(IsSelectionAvailable))]
    private bool isSelectionEnabled = true;

    /// <summary>
    /// Set by <see cref="SelectImageCard"/> when the image is loaded.
    /// </summary>
    [ObservableProperty]
    private Size currentBitmapSize = Size.Empty;

    /// <summary>
    /// True if the image file is set but the local file does not exist.
    /// </summary>
    [MemberNotNullWhen(true, nameof(NotFoundImagePath))]
    public bool IsImageFileNotFound => ImageSource?.LocalFile?.Exists == false;

    public bool IsSelectionAvailable => IsSelectionEnabled && ImageSource == null;

    /// <summary>
    /// Path of the not found image
    /// </summary>
    public string? NotFoundImagePath => ImageSource?.LocalFile?.FullPath;

    /// <inheritdoc />
    public void ApplyStep(ModuleApplyStepEventArgs e)
    {
        e.Builder.SetupImagePrimarySource(
            ImageSource ?? throw new ValidationException("Input Image is required"),
            !CurrentBitmapSize.IsEmpty
                ? CurrentBitmapSize
                : throw new ValidationException("CurrentBitmapSize is null"),
            e.Builder.Connections.BatchIndex
        );
    }

    /// <inheritdoc />
    public IEnumerable<ImageSource> GetInputImages()
    {
        if (ImageSource is { } image && !IsImageFileNotFound)
        {
            yield return image;
        }
    }

    partial void OnImageSourceChanged(ImageSource? value)
    {
        // Cache the hash for later upload use
        if (value?.LocalFile is { Exists: true } localFile)
        {
            value
                .GetBlake3HashAsync()
                .SafeFireAndForget(ex =>
                {
                    Logger.Warn(ex, "Error getting hash for image {Path}", localFile.Name);
                    notificationService.ShowPersistent(
                        $"Error getting hash for image {localFile.Name}",
                        $"{ex.GetType().Name}: {ex.Message}"
                    );
                });
        }
    }

    private static FilePickerFileType SupportedImages { get; } =
        new("Supported Images")
        {
            Patterns = new[] { "*.png", "*.jpg", "*.jpeg" },
            AppleUniformTypeIdentifiers = new[] { "public.jpeg", "public.png" },
            MimeTypes = new[] { "image/jpeg", "image/png" }
        };

    [RelayCommand]
    private async Task SelectImageFromFilePickerAsync()
    {
        var files = await App.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                FileTypeFilter = [FilePickerFileTypes.ImagePng, FilePickerFileTypes.ImageJpg, SupportedImages]
            }
        );

        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
        {
            Dispatcher.UIThread.Post(() => LoadUserImageSafe(new ImageSource(path)));
        }
    }

    /// <summary>
    /// Supports LocalImageFile Context or OS Files
    /// </summary>
    public void DragOver(object? sender, DragEventArgs e)
    {
        if (
            e.Data.GetDataFormats().Contains(DataFormats.Files)
            || e.Data.GetContext<LocalImageFile>() is not null
        )
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
        if (
            e.Data.GetFiles() is { } files
            && files.Select(f => f.TryGetLocalPath()).FirstOrDefault() is { } path
        )
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
            notificationService.Show("Error loading image", e.Message);
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
    }
}
