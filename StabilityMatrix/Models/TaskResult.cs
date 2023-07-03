using System;
using System.Diagnostics.CodeAnalysis;

namespace StabilityMatrix.Models;

public readonly record struct TaskResult<T>
{
    public readonly T? Result;
    public readonly Exception? Exception = null;

    [MemberNotNullWhen(true, nameof(Result))]
    public bool IsSuccessful => Exception is null && Result != null;
    
    public TaskResult(T? result, Exception? exception)
    {
        Result = result;
        Exception = exception;
    }
    
    public TaskResult(T? result)
    {
        Result = result;
    }

    public static TaskResult<T> FromException(Exception exception) => new(default, exception);

    public void Deconstruct(out T? result, out Exception? exception)
    {
        result = Result;
        exception = Exception;
    }
}
