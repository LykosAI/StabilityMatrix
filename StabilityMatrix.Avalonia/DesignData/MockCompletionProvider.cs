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
    public bool IsLoaded => false;

    /// <inheritdoc />
    public Func<string, string>? PrepareInsertionText => null;

    /// <inheritdoc />
    public Task LoadFromFile(FilePath path, bool recreate = false)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void BackgroundLoadFromFile(FilePath path, bool recreate = false)
    {
    }

    /// <inheritdoc />
    public IEnumerable<ICompletionData> GetCompletions(TextCompletionRequest completionRequest, int itemsCount, bool suggest)
    {
        return Array.Empty<ICompletionData>();
    }
}
