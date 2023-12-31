﻿using LiteDB;
using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Core.Database;

/// <summary>
/// Cache entry for the result of a Civit model query response
/// </summary>
public class CivitModelQueryCacheEntry
{
    // This is set as the hash of the request object (ObjectHash.GetMd5Guid)
    public Guid Id { get; set; }
    
    public DateTimeOffset? InsertedAt { get; set; }
    
    public CivitModelsRequest? Request { get; set; }
    
    [BsonRef("CivitModels")]
    public List<CivitModel>? Items { get; set; }
    
    public CivitMetadata? Metadata { get; set; }
}
