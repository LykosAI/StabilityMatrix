using System;
using System.ComponentModel;
using System.Text;
using Sentry;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(ExceptionDialog))]
[ManagedService]
[Transient]
public class ExceptionViewModel : ViewModelBase
{
    public Exception? Exception { get; set; }

    public SentryId? SentryId { get; set; }

    public string? Message => Exception?.Message;

    public string? ExceptionType => Exception?.GetType().Name ?? "";

    [Localizable(false)]
    public string? FormatAsMarkdown()
    {
        var msgBuilder = new StringBuilder();
        msgBuilder.AppendLine();

        if (Exception is not null)
        {
            msgBuilder.AppendLine("## Exception");
            msgBuilder.AppendLine($"```{ExceptionType}: {Message}```");

            if (Exception.InnerException is not null)
            {
                msgBuilder.AppendLine(
                    $"```{Exception.InnerException.GetType().Name}: {Exception.InnerException.Message}```"
                );
            }
        }
        else
        {
            msgBuilder.AppendLine("## Exception");
            msgBuilder.AppendLine("```(None)```");
        }

        if (SentryId is { } id)
        {
            msgBuilder.AppendLine("### Sentry ID");
            msgBuilder.AppendLine($"[`{id.ToString()[..8]}`]({GetIssueUrl(id)})");
        }

        if (Exception?.StackTrace is not null)
        {
            msgBuilder.AppendLine("### Stack Trace");
            msgBuilder.AppendLine($"```{Exception.StackTrace}```");
        }

        if (Exception?.InnerException is { StackTrace: not null } innerException)
        {
            msgBuilder.AppendLine($"```{innerException.StackTrace}```");
        }

        return msgBuilder.ToString();
    }

    [Localizable(false)]
    private static string GetIssueUrl(SentryId sentryId)
    {
        return $"https://stability-matrix.sentry.io/issues/?query=id%3A{sentryId.ToString()}&referrer=sm-app-ex&statsPeriod=90d";
    }
}
