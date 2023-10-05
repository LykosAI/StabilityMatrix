namespace StabilityMatrix.Core.Attributes;

/// <summary>
/// Marks that a ViewModel should have its OnLoaded and OnLoadedAsync methods called in the background
/// during MainWindow initialization, after LibraryDirectory is set.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class PreloadAttribute : Attribute { }
