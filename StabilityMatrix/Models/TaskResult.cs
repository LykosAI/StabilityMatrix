using System;
using System.Diagnostics.CodeAnalysis;

namespace StabilityMatrix.Models;

public readonly record struct TaskResult<T>
{
    public readonly T? Result;
    public readonly Exception? Exception = null;

    [MemberNotNullWhen(true, nameof(Result))]
    public bool IsSuccessful => Exception is null && Result != null;

    // ReSharper disable once MemberCanBePrivate.Global
    private TaskResult(Exception exception)
    {
        Result = default;
        Exception = exception;
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public TaskResult(T? Result)
    {
        this.Result = Result;
    }

    public TaskResult<T> FromException(Exception exception) => new(exception: exception);

    public void Deconstruct(out T? result, out Exception? exception)
    {
        result = Result;
        exception = Exception;
    }
}
