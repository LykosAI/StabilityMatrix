using System;
using System.Text.Json.Serialization;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Models.FileInterfaces;

#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Avalonia.ViewModels.Base;

public abstract partial class InferenceTabViewModelBase : LoadableViewModelBase, IDisposable, IPersistentViewProvider
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
            ((IPersistentViewProvider) this).AttachedPersistentView = null;
        }
    }
    
    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
