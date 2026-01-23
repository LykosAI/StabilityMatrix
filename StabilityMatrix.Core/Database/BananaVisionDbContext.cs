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
/// Database context for Image Lab conversations and messages.
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
        var dbPath = Path.Combine(settingsManager.LibraryDir, "ImageLab.db");

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
                        "Image Lab database collation is not Ordinal ({SortOption}), rebuilding...",
                        sortOption
                    );
                    var options = new RebuildOptions
                    {
                        Collation = new Collation(CultureInfo.InvariantCulture.LCID, CompareOptions.Ordinal),
                    };
                    db.RebuildAsync(options).GetAwaiter().GetResult();
                }

                // Run one-time migration from legacy databases
                MigrateLegacyData(db, dbPath);

                return db;
            }
            catch (AggregateException ex)
                when (ex.InnerException is LiteException e
                    && e.Message.Contains("Detected loop in FindAll", StringComparison.OrdinalIgnoreCase)
                )
            {
                logger.LogWarning(
                    "Image Lab database corruption detected ({Message}), rebuilding...",
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
                    logger.LogWarning("Failed to delete corrupt Image Lab DB: {Message}", delEx.Message);
                    break;
                }
            }
            catch (IOException ioEx)
            {
                logger.LogWarning(
                    "Image Lab database in use or not accessible ({Message}), using temporary database",
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
    /// One-time migration from legacy databases (StabilityMatrix.db and BananaVision.db) to ImageLab.db
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
                    "ImageLab.db already has {Count} conversations, skipping migration",
                    existingCount
                );
                return;
            }

            // Try migrating from BananaVision.db first (most recent legacy DB)
            var bananaVisionDbPath = Path.Combine(settingsManager.LibraryDir, "BananaVision.db");
            if (File.Exists(bananaVisionDbPath))
            {
                logger.LogInformation("Migrating data from BananaVision.db to ImageLab.db...");
                MigrateFromDatabase(newDb, bananaVisionDbPath, "BananaVision.db");
                return;
            }

            // Check for original legacy database
            var legacyDbPath = Path.Combine(settingsManager.LibraryDir, "StabilityMatrix.db");
            if (!File.Exists(legacyDbPath))
            {
                logger.LogDebug("No legacy database found, skipping migration");
                return;
            }

            logger.LogInformation("Checking legacy database for Image Lab data to migrate...");

            MigrateFromDatabase(
                newDb,
                legacyDbPath,
                "StabilityMatrix.db",
                "ImageGenerationConversations",
                "ImageGenerationMessages"
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to migrate legacy Image Lab data (this is fine, starting fresh)");
        }
    }

    /// <summary>
    /// Helper method to migrate data from a legacy database file
    /// </summary>
    private void MigrateFromDatabase(
        LiteDatabaseAsync newDb,
        string sourceDbPath,
        string sourceName,
        string conversationsCollectionName = "Conversations",
        string messagesCollectionName = "Messages"
    )
    {
        using var sourceDb = new LiteDatabaseAsync(
            new ConnectionString
            {
                Filename = sourceDbPath,
                Connection = ConnectionType.Shared,
                ReadOnly = true,
            }
        );

        var sourceConversations = sourceDb.GetCollection<ImageGenerationConversation>(
            conversationsCollectionName
        );
        var sourceMessages = sourceDb.GetCollection<ImageGenerationMessage>(messagesCollectionName);

        var conversationsList = sourceConversations.FindAllAsync().GetAwaiter().GetResult().ToList();

        if (conversationsList.Count == 0)
        {
            logger.LogDebug("No conversations found in {SourceName}, skipping migration", sourceName);
            return;
        }

        logger.LogInformation(
            "Migrating {Count} conversations from {SourceName}...",
            conversationsList.Count,
            sourceName
        );

        var conversations = newDb.GetCollection<ImageGenerationConversation>("Conversations");
        var messages = newDb.GetCollection<ImageGenerationMessage>("Messages");

        // Copy conversations
        foreach (var conversation in conversationsList)
        {
            conversations.InsertAsync(conversation).GetAwaiter().GetResult();
        }

        // Copy messages
        var messagesList = sourceMessages.FindAllAsync().GetAwaiter().GetResult().ToList();
        foreach (var message in messagesList)
        {
            messages.InsertAsync(message).GetAwaiter().GetResult();
        }

        logger.LogInformation(
            "Successfully migrated {ConvCount} conversations and {MsgCount} messages from {SourceName} to ImageLab.db",
            conversationsList.Count,
            messagesList.Count,
            sourceName
        );
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
