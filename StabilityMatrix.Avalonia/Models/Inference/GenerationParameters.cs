namespace StabilityMatrix.Avalonia.Models.Inference;

public class GenerationParameters
{
    public string PositivePrompt { get; set; }
    public string NegativePrompt { get; set; }
    public int Steps { get; set; }
    public string Sampler { get; set; }
    public double CfgScale { get; set; }
    public ulong Seed { get; set; }
    public int Height { get; set; }
    public int Width { get; set; }
    public string ModelHash { get; set; }
    public string ModelName { get; set; }
}
