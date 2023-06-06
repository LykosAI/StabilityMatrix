using System.Text.Json.Serialization;
using Refit;

namespace StabilityMatrix.Models.Api;


public class CivitModelsRequest
{
    /// <summary>
    /// The number of results to be returned per page. This can be a number between 1 and 200. By default, each page will return 100 results
    /// </summary>
    [AliasAs("limit")]
    public int? Limit { get; set; }
    
    /// <summary>
    /// The page from which to start fetching models
    /// </summary>
    [AliasAs("page")]
    public int? Page { get; set; }
    
    /// <summary>
    /// Search query to filter models by name
    /// </summary>
    [AliasAs("query")]
    public string? Query { get; set; }
    
    /// <summary>
    /// Search query to filter models by tag
    /// </summary>
    [AliasAs("tag")]
    public string? Tag { get; set; }
    
    /// <summary>
    /// Search query to filter models by user
    /// </summary>
    [AliasAs("username")]
    public string? Username { get; set; }
    
    /// <summary>
    /// The type of model you want to filter with. If none is specified, it will return all types
    /// </summary>
    [AliasAs("types")]
    public CivitModelType[]? Types { get; set; }
    
    /// <summary>
    /// The order in which you wish to sort the results
    /// </summary>
    [AliasAs("sort")]
    public CivitSortMode? Sort { get; set; }
    
    /// <summary>
    /// The time frame in which the models will be sorted
    /// </summary>
    [AliasAs("period")]
    public CivitPeriod? Period { get; set; }
    
    /// <summary>
    /// The rating you wish to filter the models with. If none is specified, it will return models with any rating
    /// </summary>
    [AliasAs("rating")]
    public int? Rating { get; set; }
    
    /// <summary>
    /// Filter to models that require or don't require crediting the creator
    /// <remarks>Requires Authentication</remarks>
    /// </summary>
    [AliasAs("favorites")]
    public bool? Favorites { get; set; }
    
    /// <summary>
    /// Filter to hidden models of the authenticated user
    /// <remarks>Requires Authentication</remarks>
    /// </summary>
    [AliasAs("hidden")]
    public bool? Hidden { get; set; }
    
    /// <summary>
    /// Only include the primary file for each model (This will use your preferred format options if you use an API token or session cookie)
    /// </summary>
    [AliasAs("primaryFileOnly")]
    public bool? PrimaryFileOnly { get; set; }
    
    /// <summary>
    /// Filter to models that allow or don't allow creating derivatives
    /// </summary>
    [AliasAs("allowDerivatives")]
    public bool? AllowDerivatives { get; set; }
    
    /// <summary>
    /// Filter to models that allow or don't allow derivatives to have a different license
    /// </summary>
    [AliasAs("allowDifferentLicenses")]
    public bool? AllowDifferentLicenses { get; set; }
    
    /// <summary>
    /// Filter to models based on their commercial permissions
    /// </summary>
    [AliasAs("allowCommercialUse")]
    public CivitCommercialUse? AllowCommercialUse { get; set; }
    
    /// <summary>
    /// If false, will return safer images and hide models that don't have safe images
    /// </summary>
    [AliasAs("nsfw")]
    public string? Nsfw { get; set; }
}
