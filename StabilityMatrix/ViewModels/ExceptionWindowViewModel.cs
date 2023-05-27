using System;

namespace StabilityMatrix.ViewModels;

public class ExceptionWindowViewModel
{
    public Exception Exception { get; set; }
    
    public string ExceptionType => Exception.GetType().Name;
}
