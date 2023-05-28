using System;

namespace StabilityMatrix.Helper;

public class EventManager
{
    public static EventManager Instance { get; } = new();

    private EventManager()
    {

    }
    
    public event EventHandler<int>? GlobalProgressChanged;
    public void OnGlobalProgressChanged(int progress) => GlobalProgressChanged?.Invoke(this, progress);
}
