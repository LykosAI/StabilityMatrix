using LiteDB.Async;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Core.Database;

/// <summary>
/// Database context for BananaVision conversations and messages.
/// Stored separately from the main StabilityMatrix.db to preserve user data
/// when the main cache database is deleted.
/// </summary>
public interface IBananaVisionDbContext : IDisposable
{
    LiteDatabaseAsync Database { get; }

    ILiteCollectionAsync<ImageGenerationConversation> Conversations { get; }
    ILiteCollectionAsync<ImageGenerationMessage> Messages { get; }
}
