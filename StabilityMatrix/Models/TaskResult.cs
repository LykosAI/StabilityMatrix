using System;

namespace StabilityMatrix.Models;

public class TaskResult<T>
{
    public T? Result { get; set; }
    public Exception? Exception { get; set; }

    public bool IsSuccessful => Exception is null && Result != null;
}
