using System;

namespace StabilityMatrix.Exceptions;

/// <summary>
/// Exception that is thrown when a process fails.
/// </summary>
public class ProcessException: Exception
{
    public ProcessException(string message) : base(message)
    {
    }
}