namespace StabilityMatrix.Core.Models;

public class GenericPostDownloadAction(Action action) : IContextAction
{
    private readonly Action? action = action;

    public object? Context { get; set; }

    public void Invoke()
    {
        action?.Invoke();
    }
}
