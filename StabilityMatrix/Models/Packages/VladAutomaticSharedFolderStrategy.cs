using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StabilityMatrix.Extensions;
using StabilityMatrix.Helper;
using StabilityMatrix.Models.FileInterfaces;

namespace StabilityMatrix.Models.Packages;

public class VladAutomaticSharedFolderStrategy : ISharedFolderStrategy
{
    private readonly ISettingsManager settingsManager;

    public VladAutomaticSharedFolderStrategy(ISettingsManager settingsManager)
    {
        this.settingsManager = settingsManager;
    }

    public async Task ExecuteAsync(BasePackage package)
    {
        var installedPackage = settingsManager
            .Settings
            .InstalledPackages
            .Single(p => p.PackageName == package.Name);
        var configFilePath = Path.Combine(settingsManager.LibraryDir, installedPackage.LibraryPath!, "config.json");
        
        // Load the configuration file
        var json = await File.ReadAllTextAsync(configFilePath);
        var job = JsonConvert.DeserializeObject<JObject>(json)!;
        
        // Update the configuration values
        var modelsDirectory = new DirectoryPath(settingsManager.ModelsDirectory);
        foreach (var (sharedFolderType, configKey) in map)
        {
            var value = Path.Combine(modelsDirectory.FullPath, sharedFolderType.GetStringValue());
            job[configKey] = value;
        }
        
        // Write the configuration file
        await File.WriteAllTextAsync(configFilePath, JsonConvert.SerializeObject(job, Formatting.Indented));
    }

    private Dictionary<SharedFolderType, string> map = new()
    {
        { SharedFolderType.StableDiffusion, "ckpt_dir" },
        { SharedFolderType.Diffusers, "diffusers_dir" },
        { SharedFolderType.VAE, "vae_dir" },
        { SharedFolderType.Lora, "lora_dir" },
        { SharedFolderType.LyCORIS, "lyco_dir" },
        // { SharedFolderType.Styles, "styles_dir"},
        { SharedFolderType.TextualInversion, "embeddings_dir" },
        { SharedFolderType.Hypernetwork, "hypernetwork_dir" },
        { SharedFolderType.Codeformer, "codeformer_models_path" },
        { SharedFolderType.GFPGAN, "gfpgan_models_path" },
        { SharedFolderType.ESRGAN, "esrgan_models_path" },
        { SharedFolderType.BSRGAN , "bsrgan_models_path"},
        { SharedFolderType.RealESRGAN, "realesrgan_models_path" },
        { SharedFolderType.ScuNET, "scunet_models_path" },
        { SharedFolderType.SwinIR, "swinir_models_path" },
        { SharedFolderType.LDSR, "ldsr_models_path" },
        { SharedFolderType.CLIP, "clip_models_path" }
    };
} 

