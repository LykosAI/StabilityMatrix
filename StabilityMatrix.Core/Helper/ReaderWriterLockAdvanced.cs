namespace StabilityMatrix.Core.Helper;

/// <summary>
/// Extended <see cref="ReaderWriterLockSlim"/> with support for disposal pattern.
/// </summary>
public class ReaderWriterLockAdvanced : ReaderWriterLockSlim
{
    public ReaderWriterLockAdvanced()
    {
    }
    
    public ReaderWriterLockAdvanced(LockRecursionPolicy recursionPolicy) : base(recursionPolicy)
    {
    }
    
    public DisposableLock EnterReadContext(TimeSpan timeout = default)
    {
        if (!TryEnterReadLock(timeout))
        {
            throw new TimeoutException("Timeout waiting for read lock");
        }
        return new DisposableLock(this, LockType.Read);
    }
    
    public DisposableLock EnterWriteContext(TimeSpan timeout = default)
    {
        if (!TryEnterWriteLock(timeout))
        {
            throw new TimeoutException("Timeout waiting for write lock");
        }
        return new DisposableLock(this, LockType.Write);
    }
    
    public DisposableLock EnterUpgradeableReadContext(TimeSpan timeout = default)
    {
        if (!TryEnterUpgradeableReadLock(timeout))
        {
            throw new TimeoutException("Timeout waiting for upgradeable read lock");
        }
        return new DisposableLock(this, LockType.UpgradeableRead);
    }
}

/// <summary>
/// Wrapper for disposable lock
/// </summary>
public class DisposableLock : IDisposable
{
    private readonly ReaderWriterLockAdvanced readerWriterLock;
    private readonly LockType lockType;
    
    public DisposableLock(ReaderWriterLockAdvanced @lock, LockType lockType)
    {
        readerWriterLock = @lock;
        this.lockType = lockType;
    }

    public DisposableLock UpgradeToWrite(TimeSpan timeout = default)
    {
        if (lockType != LockType.UpgradeableRead)
        {
            throw new InvalidOperationException("Can only upgrade from upgradeable read lock");
        }
        return readerWriterLock.EnterWriteContext(timeout);
    }

    public void Dispose()
    {
        switch (lockType)
        {
            case LockType.Read:
                readerWriterLock.ExitReadLock();
                break;
            case LockType.Write:
                readerWriterLock.ExitWriteLock();
                break;
            case LockType.UpgradeableRead:
                readerWriterLock.ExitUpgradeableReadLock();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        GC.SuppressFinalize(this);
    }
}

public enum LockType
{
    Read,
    Write,
    UpgradeableRead
}
