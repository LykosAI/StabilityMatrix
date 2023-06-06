using System.Text.Json.Serialization;

namespace StabilityMatrix.Models.Api;


public class CivitMetadata
{
    [JsonPropertyName("totalItems")]
    public int TotalItems { get; set; }
    
    [JsonPropertyName("currentPage")]
    public int CurrentPage { get; set; }
    
    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }
    
    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }
    
    [JsonPropertyName("nextPage")]
    public string? NextPage { get; set; }
    
    [JsonPropertyName("prevPage")]
    public string? PrevPage { get; set; }
}
