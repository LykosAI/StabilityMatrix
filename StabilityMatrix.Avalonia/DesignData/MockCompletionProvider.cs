using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StabilityMatrix.Avalonia.Controls.CodeCompletion;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockCompletionProvider : ICompletionProvider
{
    /// <inheritdoc />
    public CompletionType AvailableCompletionTypes { get; set; }

    /// <inheritdoc />
    public Func<ICompletionData, string>? PrepareInsertionText { get; } = data => data.Text;

    /// <inheritdoc />
    public Task LoadFromFile(FilePath path, bool recreate = false)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void BackgroundLoadFromFile(FilePath path, bool recreate = false) { }

    /// <inheritdoc />
    public IEnumerable<ICompletionData> GetCompletions(
        TextCompletionRequest completionRequest,
        int itemsCount,
        bool suggest
    )
    {
        return Array.Empty<ICompletionData>();
    }
}
