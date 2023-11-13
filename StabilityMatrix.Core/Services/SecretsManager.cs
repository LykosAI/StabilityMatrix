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
            logger.LogWarning(
                e,
                "Failed to load secrets ({ExcType}), saving new instance",
                e.GetType().Name
            );

            var secrets = new Secrets();
            await SaveAsync(secrets).ConfigureAwait(false);

            return secrets;
        }
    }

    /// <inheritdoc />
    public Task SaveAsync(Secrets secrets)
    {
        var fileBytes = GlobalEncryptedSerializer.Serialize(secrets);
        return GlobalFile.WriteAllBytesAsync(fileBytes);
    }
}
