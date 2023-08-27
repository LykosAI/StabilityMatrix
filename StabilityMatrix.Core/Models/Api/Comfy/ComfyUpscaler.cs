namespace StabilityMatrix.Core.Models.Api.Comfy;

public readonly record struct ComfyUpscaler(string Name, ComfyUpscalerType Type)
{
    private static Dictionary<string, string> ConvertDict { get; } =
        new()
        {
            ["nearest-exact"] = "Nearest Exact",
            ["bilinear"] = "Bilinear",
            ["area"] = "Area",
            ["bicubic"] = "Bicubic",
            ["bislerp"] = "Bislerp",
        };

    public string DisplayType
    {
        get
        {
            return Type switch
            {
                ComfyUpscalerType.Latent => "Latent",
                ComfyUpscalerType.ESRGAN => "ESRGAN",
                ComfyUpscalerType.None => "None",
                _ => throw new ArgumentOutOfRangeException(nameof(Type), Type, null)
            };
        }
    }
    
    public string DisplayName
    {
        get
        {
            if (Type == ComfyUpscalerType.Latent)
            {
                return ConvertDict.TryGetValue(Name, out var displayName) ? displayName : Name;
            }
            
            return Name;
        }
    }
    
    public string ShortDisplayName
    {
        get
        {
            if (Type == ComfyUpscalerType.ESRGAN)
            {
                // Remove file extensions
                return Path.GetFileNameWithoutExtension(Name);
            }
            
            return DisplayName;
        }
    }
}
