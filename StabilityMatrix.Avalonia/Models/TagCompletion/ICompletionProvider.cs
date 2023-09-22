using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StabilityMatrix.Avalonia.Controls.CodeCompletion;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Avalonia.Models.TagCompletion;

public interface ICompletionProvider
{
    /// <summary>
    /// Currently available completion types.
    /// </summary>
    CompletionType AvailableCompletionTypes { get; }

    /// <summary>
    /// Optional function to transform the text to be inserted
    /// </summary>
    Func<ICompletionData, string>? PrepareInsertionText => null;

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
        TextCompletionRequest completionRequest,
        int itemsCount,
        bool suggest
    );
}
