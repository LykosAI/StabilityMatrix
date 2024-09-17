using System.Diagnostics.CodeAnalysis;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Extensions;

public static class ProgressExtensions
{
    [return: NotNullIfNotNull(nameof(progress))]
    public static Action<ProcessOutput>? AsProcessOutputHandler(
        this IProgress<ProgressReport>? progress,
        bool setMessageAsOutput = true
    )
    {
        if (progress is null)
        {
            return null;
        }

        return output =>
        {
            progress.Report(
                new ProgressReport
                {
                    Progress = -1f,
                    IsIndeterminate = true,
                    Message = setMessageAsOutput ? output.Text : null,
                    ProcessOutput = output,
                    PrintToConsole = true
                }
            );
        };
    }
}
