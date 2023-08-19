using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StabilityMatrix.Avalonia.Controls.CodeCompletion;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Avalonia.Models.TagCompletion;

public interface ICompletionProvider
{
    /// <summary>
    /// Whether the completion provider is loaded.
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    /// Optional function to transform the text to be inserted
    /// </summary>
    Func<string, string>? PrepareInsertionText => null;
    
    /// <summary>
    /// Load the completion provider from a file.
    /// </summary>
    Task LoadFromFile(FilePath path, bool recreate = false);

    /// <summary>
    /// Load the completion provider from a file in the background.
    /// </summary>
    void BackgroundLoadFromFile(FilePath path, bool recreate = false);
    
    /// <summary>
    /// Returns a list of completion items for the given text.
    /// </summary>
    public IEnumerable<ICompletionData> GetCompletions(
        string searchTerm,
        int itemsCount,
        bool suggest
    );
}
