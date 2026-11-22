using System.Collections.Generic;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Models.Inference;

/// <summary>
/// Produces the errors to show in a prompt editor.
/// </summary>
/// <param name="tokenizer">Tokenizer for the prompt grammar.</param>
/// <param name="modelIndex">When set, extra networks must exist in the local model index.</param>
public sealed class PromptDiagnosticsProvider(
    ITokenizerProvider tokenizer,
    IModelIndexService? modelIndex = null
)
{
    public IReadOnlyList<PromptError> GetDiagnostics(string text) =>
        Prompt.FromRawText(text, tokenizer).GetDiagnostics(modelIndex);
}
