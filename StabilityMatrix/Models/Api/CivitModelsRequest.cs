using System.Text.Json.Serialization;

namespace StabilityMatrix.Models.Api;


public class CivitModelsRequest
{
    /// <summary>
    /// The number of results to be returned per page. This can be a number between 1 and 200. By default, each page will return 100 results
    /// </summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; set; }
    
    /// <summary>
    /// The page from which to start fetching models
    /// </summary>
    [JsonPropertyName("page")]
    public int? Page { get; set; }
    
    /// <summary>
    /// Search query to filter models by name
    /// </summary>
    [JsonPropertyName("query")]
    public string? Query { get; set; }
    
    /// <summary>
    /// Search query to filter models by tag
    /// </summary>
    [JsonPropertyName("tag")]
    public string? Tag { get; set; }
    
    /// <summary>
    /// Search query to filter models by user
    /// </summary>
    [JsonPropertyName("username")]
    public string? Username { get; set; }
    
    /// <summary>
    /// The type of model you want to filter with. If none is specified, it will return all types
    /// </summary>
    [JsonPropertyName("types")]
    public CivitModelType[]? Types { get; set; }
    
    /// <summary>
    /// The order in which you wish to sort the results
    /// </summary>
    [JsonPropertyName("sort")]
    public CivitSortMode? Sort { get; set; }
    
    /// <summary>
    /// The time frame in which the models will be sorted
    /// </summary>
    [JsonPropertyName("period")]
    public CivitPeriod? Period { get; set; }
    
    /// <summary>
    /// The rating you wish to filter the models with. If none is specified, it will return models with any rating
    /// </summary>
    [JsonPropertyName("rating")]
    public int? Rating { get; set; }
    
    /// <summary>
    /// Filter to models that require or don't require crediting the creator
    /// <remarks>Requires Authentication</remarks>
    /// </summary>
    [JsonPropertyName("favorites")]
    public bool? Favorites { get; set; }
    
    /// <summary>
    /// Filter to hidden models of the authenticated user
    /// <remarks>Requires Authentication</remarks>
    /// </summary>
    [JsonPropertyName("hidden")]
    public bool? Hidden { get; set; }
    
    /// <summary>
    /// Only include the primary file for each model (This will use your preferred format options if you use an API token or session cookie)
    /// </summary>
    [JsonPropertyName("primaryFileOnly")]
    public bool? PrimaryFileOnly { get; set; }
    
    /// <summary>
    /// Filter to models that allow or don't allow creating derivatives
    /// </summary>
    [JsonPropertyName("allowDerivatives")]
    public bool? AllowDerivatives { get; set; }
    
    /// <summary>
    /// Filter to models that allow or don't allow derivatives to have a different license
    /// </summary>
    [JsonPropertyName("allowDifferentLicenses")]
    public bool? AllowDifferentLicenses { get; set; }
    
    /// <summary>
    /// Filter to models based on their commercial permissions
    /// </summary>
    [JsonPropertyName("allowCommercialUse")]
    public CivitCommercialUse? AllowCommercialUse { get; set; }
    
    /// <summary>
    /// If false, will return safer images and hide models that don't have safe images
    /// </summary>
    [JsonPropertyName("nsfw")]
    public bool? Nsfw { get; set; }
}
