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
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Database;
using Size = System.Drawing.Size;
#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(SelectImageCard))]
[ManagedService]
[RegisterTransient<SelectImageCardViewModel>]
public partial class SelectImageCardViewModel(
    INotificationService notificationService,
    ServiceManager<ViewModelBase> vmFactory
) : LoadableViewModelBase, IDropTarget, IComfyStep, IInputImageProvider
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static FilePickerFileType SupportedImages { get; } =
        new("Supported Images")
        {
            Patterns = new[] { "*.png", "*.jpg", "*.jpeg" },
            AppleUniformTypeIdentifiers = new[] { "public.jpeg", "public.png" },
            MimeTypes = new[] { "image/jpeg", "image/png" }
        };

    private readonly Lazy<MaskEditorViewModel> _lazyMaskEditorViewModel =
        new(vmFactory.Get<MaskEditorViewModel>);

    /// <summary>
    /// When true, enables a button to open a mask editor for the image.
    /// This is not saved or loaded from state.
    /// </summary>
    [ObservableProperty]
    [property: JsonIgnore]
    [property: MemberNotNull(nameof(MaskEditorViewModel))]
    private bool isMaskEditorEnabled;

    /// <summary>
    /// Toggles whether the mask overlay is shown over the image.
    /// </summary>
    [ObservableProperty]
    private bool isMaskOverlayEnabled;

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

    [JsonInclude]
    public MaskEditorViewModel? MaskEditorViewModel =>
        IsMaskEditorEnabled ? _lazyMaskEditorViewModel.Value : null;

    [JsonIgnore]
    public ImageSource? LastMaskImage { get; private set; }

    /// <inheritdoc />
    public void ApplyStep(ModuleApplyStepEventArgs e)
    {
        // With Mask image
        if (IsMaskEditorEnabled && MaskEditorViewModel.IsMaskEnabled)
        {
            MaskEditorViewModel.PaintCanvasViewModel.CanvasSize = CurrentBitmapSize;

            e.Builder.SetupImagePrimarySourceWithMask(
                ImageSource ?? throw new ValidationException("Input Image is required"),
                !CurrentBitmapSize.IsEmpty
                    ? CurrentBitmapSize
                    : throw new ValidationException("CurrentBitmapSize is null"),
                MaskEditorViewModel.GetCachedOrNewMaskRenderInverseAlphaImage(),
                MaskEditorViewModel.PaintCanvasViewModel.CanvasSize,
                e.Builder.Connections.BatchIndex
            );
        }
        // Normal image only
        else
        {
            e.Builder.SetupImagePrimarySource(
                ImageSource ?? throw new ValidationException("Input Image is required"),
                !CurrentBitmapSize.IsEmpty
                    ? CurrentBitmapSize
                    : throw new ValidationException("CurrentBitmapSize is null"),
                e.Builder.Connections.BatchIndex
            );
        }
    }

    /// <inheritdoc />
    public IEnumerable<ImageSource> GetInputImages()
    {
        // Main image
        if (ImageSource is { } image && !IsImageFileNotFound)
        {
            yield return image;
        }

        // Mask image
        if (IsMaskEditorEnabled && MaskEditorViewModel.IsMaskEnabled)
        {
            using var timer = CodeTimer.StartDebug("MaskImage");

            MaskEditorViewModel.PaintCanvasViewModel.CanvasSize = CurrentBitmapSize;

            var maskImage = MaskEditorViewModel.GetCachedOrNewMaskRenderInverseAlphaImage();

            timer.Dispose();

            yield return maskImage;
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

    [RelayCommand]
    private async Task OpenEditMaskDialogAsync()
    {
        if (!IsMaskEditorEnabled || ImageSource is null)
        {
            return;
        }

        // Make a backup to restore if not saving later
        var maskEditorStateBackup = MaskEditorViewModel.SaveStateToJsonObject();

        // Set the background image
        if (await ImageSource.GetBitmapAsync() is not { } currentBitmap)
        {
            Logger.Warn("GetBitmapAsync returned null for image {Path}", ImageSource.LocalFile?.FullPath);
            return;
        }
        MaskEditorViewModel.PaintCanvasViewModel.BackgroundImage = currentBitmap.ToSKBitmap();

        if (await MaskEditorViewModel.GetDialog().ShowAsync() == ContentDialogResult.Primary)
        {
            MaskEditorViewModel.InvalidateCachedMaskRenderImage();
        }
        else
        {
            // Restore the backup
            MaskEditorViewModel.LoadStateFromJsonObject(maskEditorStateBackup);
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
