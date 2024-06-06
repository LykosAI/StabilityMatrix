using System.Diagnostics.CodeAnalysis;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Extensions;

public static class ProgressExtensions
{
    [return: NotNullIfNotNull(nameof(progress))]
    public static Action<ProcessOutput>? AsProcessOutputHandler(this IProgress<ProgressReport>? progress)
    {
        return progress == null
            ? null
            : output =>
            {
                progress.Report(
                    new ProgressReport
                    {
                        IsIndeterminate = true,
                        Message = output.Text,
                        ProcessOutput = output,
                        PrintToConsole = true
                    }
                );
            };
    }
}
