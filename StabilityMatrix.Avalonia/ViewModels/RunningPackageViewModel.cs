using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using SymbolIconSource = FluentIcons.Avalonia.Fluent.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(ConsoleOutputPage))]
public class RunningPackageViewModel(PackagePair runningPackage, ConsoleViewModel console) : PageViewModelBase
{
    public PackagePair RunningPackage { get; } = runningPackage;
    public ConsoleViewModel Console { get; } = console;

    public override string Title => RunningPackage.InstalledPackage.PackageName ?? "Running Package";
    public override IconSource IconSource => new SymbolIconSource();
}
