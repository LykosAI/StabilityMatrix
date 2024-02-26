using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(ConsoleOutputPage))]
public class RunningPackageViewModel(PackagePair runningPackage, ConsoleViewModel console) : ViewModelBase
{
    public PackagePair RunningPackage { get; } = runningPackage;
    public ConsoleViewModel Console { get; } = console;
}
