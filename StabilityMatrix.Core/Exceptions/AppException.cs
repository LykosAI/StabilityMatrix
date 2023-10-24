namespace StabilityMatrix.Core.Exceptions;

/// <summary>
/// Generic runtime exception with custom handling by notification service
/// </summary>
public class AppException : ApplicationException
{
    public string? Details { get; init; }
}
