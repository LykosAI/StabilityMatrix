using System;
using Sentry;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(ExceptionDialog))]
[ManagedService]
[Transient]
public partial class ExceptionViewModel : ViewModelBase
{
    public Exception? Exception { get; set; }

    public SentryId? SentryId { get; set; }

    public string? Message => Exception?.Message;

    public string? ExceptionType => Exception?.GetType().Name ?? "";

    public string? FormatAsMarkdown()
    {
        if (Exception is null)
        {
            return null;
        }

        var message = $"## Exception\n{ExceptionType}: {Message}\n";

        if (SentryId is not null)
        {
            message += $"### Sentry ID\n```\n{SentryId}\n```\n";
        }

        if (Exception.StackTrace != null)
        {
            message += $"### Stack Trace\n```\n{Exception.StackTrace}\n```\n";
        }

        if (Exception.InnerException is { } innerException)
        {
            message += $"## Inner Exception\n{innerException.GetType().Name}: {innerException.Message}\n";

            if (innerException.StackTrace != null)
            {
                message += $"### Stack Trace\n```\n{innerException.StackTrace}\n```\n";
            }
        }

        return message;
    }
}
