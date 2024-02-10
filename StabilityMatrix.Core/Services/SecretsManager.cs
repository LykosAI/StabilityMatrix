using System.Reactive.Concurrency;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Core.Services;

/// <summary>
/// Default implementation of <see cref="ISecretsManager"/>.
/// Data is encrypted at rest in %APPDATA%\StabilityMatrix\user-secrets.data
/// </summary>
[Singleton(typeof(ISecretsManager))]
public class SecretsManager : ISecretsManager
{
    private readonly ILogger<SecretsManager> logger;

    private static FilePath GlobalFile => GlobalConfig.HomeDir.JoinFile("user-secrets.data");

    private static SemaphoreSlim GlobalFileLock { get; } = new(1, 1);

    public SecretsManager(ILogger<SecretsManager> logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<Secrets> LoadAsync()
    {
        if (!GlobalFile.Exists)
        {
            return new Secrets();
        }

        var fileBytes = await GlobalFile.ReadAllBytesAsync().ConfigureAwait(false);
        return GlobalEncryptedSerializer.Deserialize<Secrets>(fileBytes);
    }

    /// <inheritdoc />
    public async Task<Secrets> SafeLoadAsync()
    {
        try
        {
            return await LoadAsync().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to load secrets ({ExcType}), saving new instance", e.GetType().Name);

            var secrets = new Secrets();
            await SaveAsync(secrets).ConfigureAwait(false);

            return secrets;
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(Secrets secrets)
    {
        await GlobalFileLock.WaitAsync().ConfigureAwait(false);

        try
        {
            var fileBytes = GlobalEncryptedSerializer.Serialize(secrets);
            await GlobalFile.WriteAllBytesAsync(fileBytes).ConfigureAwait(false);
        }
        finally
        {
            GlobalFileLock.Release();
        }
    }
}
