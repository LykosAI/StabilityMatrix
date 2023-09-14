using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;

#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Avalonia.ViewModels.Base;

public abstract partial class InferenceTabViewModelBase
    : LoadableViewModelBase,
        IDisposable,
        IPersistentViewProvider,
        IDropTarget
{
    /// <summary>
    /// The title of the tab
    /// </summary>
    public virtual string TabTitle => ProjectFile?.NameWithoutExtension ?? "New Project";

    /// <summary>
    /// Whether there are unsaved changes
    /// </summary>
    [ObservableProperty]
    [property: JsonIgnore]
    private bool hasUnsavedChanges;

    /// <summary>
    /// The tab's project file
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TabTitle))]
    [property: JsonIgnore]
    private FilePath? projectFile;

    /// <inheritdoc />
    Control? IPersistentViewProvider.AttachedPersistentView { get; set; }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            ((IPersistentViewProvider)this).AttachedPersistentView = null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
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
                    var metadata = imageFile.ReadMetadata();
                    if (metadata.SMProject is not null)
                    {
                        var project = JsonSerializer.Deserialize<InferenceProjectDocument>(
                            metadata.SMProject
                        );

                        // Check project type matches
                        if (project?.GetViewModelType() == GetType() && project.State is not null)
                        {
                            LoadStateFromJsonObject(project.State);
                        }

                        // Load image
                        if (this is IImageGalleryComponent imageGalleryComponent)
                        {
                            imageGalleryComponent.LoadImagesToGallery(
                                new ImageSource(imageFile.GlobalFullPath)
                            );
                        }
                    }
                });

                return;
            }
        }
        // 2. OS Files
        if (e.Data.GetDataFormats().Contains(DataFormats.Files))
        {
            e.Handled = true;
        }
    }
}
