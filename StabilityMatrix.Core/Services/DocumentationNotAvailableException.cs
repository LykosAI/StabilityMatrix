using System;

namespace StabilityMatrix.Core.Services;

/// <summary>
/// Thrown when the documentation folder is not present in the source repository yet.
/// </summary>
public class DocumentationNotAvailableException : Exception
{
    public DocumentationNotAvailableException(string message)
        : base(message) { }

    public DocumentationNotAvailableException(string message, Exception innerException)
        : base(message, innerException) { }
}
