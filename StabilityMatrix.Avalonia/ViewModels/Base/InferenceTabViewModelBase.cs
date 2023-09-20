using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
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

    #region Weak Events

    private WeakEventManager<LoadViewStateEventArgs>? loadViewStateRequestedEventManager;

    public event EventHandler<LoadViewStateEventArgs> LoadViewStateRequested
    {
        add
        {
            loadViewStateRequestedEventManager ??= new WeakEventManager<LoadViewStateEventArgs>();
            loadViewStateRequestedEventManager.AddEventHandler(value);
        }
        remove => loadViewStateRequestedEventManager?.RemoveEventHandler(value);
    }

    protected void LoadViewState(LoadViewStateEventArgs args) =>
        loadViewStateRequestedEventManager?.RaiseEvent(this, args, nameof(LoadViewStateRequested));

    private WeakEventManager<SaveViewStateEventArgs>? saveViewStateRequestedEventManager;

    public event EventHandler<SaveViewStateEventArgs> SaveViewStateRequested
    {
        add
        {
            saveViewStateRequestedEventManager ??= new WeakEventManager<SaveViewStateEventArgs>();
            saveViewStateRequestedEventManager.AddEventHandler(value);
        }
        remove => saveViewStateRequestedEventManager?.RemoveEventHandler(value);
    }

    protected async Task<ViewState> SaveViewState()
    {
        var eventArgs = new SaveViewStateEventArgs();
        saveViewStateRequestedEventManager?.RaiseEvent(
            this,
            eventArgs,
            nameof(SaveViewStateRequested)
        );

        if (eventArgs.StateTask is not { } stateTask)
        {
            throw new InvalidOperationException(
                "SaveViewStateRequested event handler did not set the StateTask property"
            );
        }

        return await stateTask;
    }

    #endregion

    [RelayCommand]
    private async Task DebugSaveViewState()
    {
        var state = await SaveViewState();
        if (state.DockLayout is { } layout)
        {
            await DialogHelper.CreateJsonDialog(layout).ShowAsync();
        }
        else
        {
            await DialogHelper.CreateTaskDialog("Failed", "No layout data").ShowAsync();
        }
    }

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
            e.DragEffects = DragDropEffects.None;
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
                        if (
                            project?.ProjectType.ToViewModelType() == GetType()
                            && project.State is not null
                        )
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
