namespace StabilityMatrix.Core.Models.Api;

public class CivitFileHashes
{
    public string? SHA256 { get; set; }
    
    public string? CRC32 { get; set; }
    
    public string? BLAKE3 { get; set; }
}
