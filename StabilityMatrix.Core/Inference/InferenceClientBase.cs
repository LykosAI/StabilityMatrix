namespace StabilityMatrix.Core.Inference;

public abstract class InferenceClientBase : IDisposable
{
    /// <summary>
    /// Start the connection
    /// </summary>
    public virtual Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Close the connection to remote resources
    /// </summary>
    public virtual Task CloseAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
    
    protected virtual void Dispose(bool disposing)
    {
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
