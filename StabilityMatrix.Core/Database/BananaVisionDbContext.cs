using System.Globalization;
using Injectio.Attributes;
using LiteDB;
using LiteDB.Async;
using LiteDB.Engine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StabilityMatrix.Core.Models.Configs;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Database;

/// <summary>
/// Database context for BananaVision conversations and messages.
/// Stored separately from the main StabilityMatrix.db to preserve user data
/// when the main cache database is deleted.
/// </summary>
[RegisterSingleton<IBananaVisionDbContext, BananaVisionDbContext>]
public class BananaVisionDbContext : IBananaVisionDbContext
{
    private readonly ILogger<BananaVisionDbContext> logger;
    private readonly ISettingsManager settingsManager;
    private readonly DebugOptions debugOptions;

    private readonly Lazy<LiteDatabaseAsync> lazyDatabase;
    private bool migrationAttempted;

    public LiteDatabaseAsync Database => lazyDatabase.Value;

    // Collections
    public ILiteCollectionAsync<ImageGenerationConversation> Conversations =>
        Database.GetCollection<ImageGenerationConversation>("Conversations");

    public ILiteCollectionAsync<ImageGenerationMessage> Messages =>
        Database.GetCollection<ImageGenerationMessage>("Messages");

    public BananaVisionDbContext(
        ILogger<BananaVisionDbContext> logger,
        ISettingsManager settingsManager,
        IOptions<DebugOptions> debugOptions
    )
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
        this.debugOptions = debugOptions.Value;

        lazyDatabase = new Lazy<LiteDatabaseAsync>(CreateDatabase);
    }

    private LiteDatabaseAsync CreateDatabase()
    {
        const int maxAttempts = 2;
        var dbPath = Path.Combine(settingsManager.LibraryDir, "BananaVision.db");

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            LiteDatabaseAsync? db = null;

            try
            {
                if (debugOptions.TempDatabase)
                {
                    db = new LiteDatabaseAsync(":temp:");
                    return db;
                }

                db = new LiteDatabaseAsync(
                    new ConnectionString { Filename = dbPath, Connection = ConnectionType.Shared }
                );

                var sortOption = db.Collation.SortOptions;
                if (sortOption is not CompareOptions.Ordinal)
                {
                    logger.LogDebug(
                        "BananaVision database collation is not Ordinal ({SortOption}), rebuilding...",
                        sortOption
                    );
                    var options = new RebuildOptions
                    {
                        Collation = new Collation(CultureInfo.InvariantCulture.LCID, CompareOptions.Ordinal),
                    };
                    db.RebuildAsync(options).GetAwaiter().GetResult();
                }

                // Run one-time migration from legacy database
                MigrateLegacyData(db, dbPath);

                return db;
            }
            catch (AggregateException ex)
                when (ex.InnerException is LiteException e
                    && e.Message.Contains("Detected loop in FindAll", StringComparison.OrdinalIgnoreCase)
                )
            {
                logger.LogWarning(
                    "BananaVision database corruption detected ({Message}), rebuilding...",
                    e.Message
                );

                try
                {
                    db?.Dispose();
                }
                catch
                {
                    // ignored
                }

                try
                {
                    // Backup then delete
                    var corruptPath = dbPath + ".old-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    if (File.Exists(dbPath))
                    {
                        File.Copy(dbPath, corruptPath, overwrite: false);
                        File.Delete(dbPath);
                    }
                }
                catch (Exception delEx)
                {
                    logger.LogWarning("Failed to delete corrupt BananaVision DB: {Message}", delEx.Message);
                    break;
                }
            }
            catch (IOException ioEx)
            {
                logger.LogWarning(
                    "BananaVision database in use or not accessible ({Message}), using temporary database",
                    ioEx.Message
                );
                break;
            }
        }

        // Fallback to temporary database
        var tempDb = new LiteDatabaseAsync(":temp:");
        return tempDb;
    }

    /// <summary>
    /// One-time migration from legacy StabilityMatrix.db to BananaVision.db
    /// </summary>
    private void MigrateLegacyData(LiteDatabaseAsync newDb, string newDbPath)
    {
        if (migrationAttempted)
            return;

        migrationAttempted = true;

        try
        {
            // Check if new database already has data
            var conversations = newDb.GetCollection<ImageGenerationConversation>("Conversations");
            var existingCount = conversations.CountAsync().GetAwaiter().GetResult();

            if (existingCount > 0)
            {
                logger.LogDebug(
                    "BananaVision.db already has {Count} conversations, skipping migration",
                    existingCount
                );
                return;
            }

            // Check for legacy database
            var legacyDbPath = Path.Combine(settingsManager.LibraryDir, "StabilityMatrix.db");
            if (!File.Exists(legacyDbPath))
            {
                logger.LogDebug("No legacy database found, skipping migration");
                return;
            }

            logger.LogInformation("Checking legacy database for BananaVision data to migrate...");

            using var legacyDb = new LiteDatabaseAsync(
                new ConnectionString
                {
                    Filename = legacyDbPath,
                    Connection = ConnectionType.Shared,
                    ReadOnly = true,
                }
            );

            var legacyConversations = legacyDb.GetCollection<ImageGenerationConversation>(
                "ImageGenerationConversations"
            );
            var legacyMessages = legacyDb.GetCollection<ImageGenerationMessage>("ImageGenerationMessages");

            var conversationsList = legacyConversations.FindAllAsync().GetAwaiter().GetResult().ToList();

            if (conversationsList.Count == 0)
            {
                logger.LogDebug("No legacy conversations found, skipping migration");
                return;
            }

            logger.LogInformation(
                "Migrating {Count} conversations from legacy database...",
                conversationsList.Count
            );

            var messages = newDb.GetCollection<ImageGenerationMessage>("Messages");

            // Copy conversations
            foreach (var conversation in conversationsList)
            {
                conversations.InsertAsync(conversation).GetAwaiter().GetResult();
            }

            // Copy messages
            var messagesList = legacyMessages.FindAllAsync().GetAwaiter().GetResult().ToList();
            foreach (var message in messagesList)
            {
                messages.InsertAsync(message).GetAwaiter().GetResult();
            }

            logger.LogInformation(
                "Successfully migrated {ConvCount} conversations and {MsgCount} messages to BananaVision.db",
                conversationsList.Count,
                messagesList.Count
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to migrate legacy BananaVision data (this is fine, starting fresh)"
            );
        }
    }

    public void Dispose()
    {
        if (lazyDatabase.IsValueCreated)
        {
            Database.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
