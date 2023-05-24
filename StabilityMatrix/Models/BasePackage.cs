namespace StabilityMatrix.Models;

public abstract class BasePackage
{
    public abstract string Name { get; }
    public abstract string DisplayName { get; }
    public abstract string Author { get; }
    
    public string ByAuthor => $"By {Author}";
}
