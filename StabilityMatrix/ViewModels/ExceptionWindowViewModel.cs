using System;
using System.Windows;
using CommunityToolkit.Mvvm.Input;

namespace StabilityMatrix.ViewModels;

public class ExceptionWindowViewModel
{
    public Exception Exception { get; set; }
    
    public string ExceptionType => Exception.GetType().Name;
}
