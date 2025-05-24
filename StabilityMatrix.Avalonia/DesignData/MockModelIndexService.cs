using Nito.Disposables.Internals;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockModelIndexService : IModelIndexService
{
    /// <inheritdoc />
    public Dictionary<SharedFolderType, List<LocalModelFile>> ModelIndex { get; } =
        new()
        {
            [SharedFolderType.StableDiffusion] =
            [
                new LocalModelFile
                {
                    SharedFolderType = SharedFolderType.StableDiffusion,
                    RelativePath = "art_shaper_v8.safetensors",
                    PreviewImageFullPath =
                        "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/dd9b038c-bd15-43ab-86ab-66e145ad7ff2/width=512/img.jpeg",
                    ConnectedModelInfo = new ConnectedModelInfo
                    {
                        ModelName = "Art Shaper (very long name example)",
                        VersionName = "Style v8 (very long name)",
                    },
                },
                new LocalModelFile
                {
                    SharedFolderType = SharedFolderType.StableDiffusion,
                    RelativePath = "background_arts.safetensors",
                    PreviewImageFullPath =
                        "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/71c81ddf-d8c3-46b4-843d-9f8f20a9254a/width=512/img.jpeg",
                    ConnectedModelInfo = new ConnectedModelInfo
                    {
                        ModelName = "Background Arts",
                        VersionName = "Anime Style v10",
                    },
                },
            ],
            [SharedFolderType.Lora] =
            [
                new LocalModelFile
                {
                    SharedFolderType = SharedFolderType.Lora,
                    RelativePath = "Lora/mock_model_1.safetensors",
                },
                new LocalModelFile
                {
                    SharedFolderType = SharedFolderType.Lora,
                    RelativePath = "Lora/mock_model_2.safetensors",
                },
                new LocalModelFile
                {
                    SharedFolderType = SharedFolderType.Lora,
                    RelativePath = "Lora/background_arts.safetensors",
                    PreviewImageFullPath =
                        "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/71c81ddf-d8c3-46b4-843d-9f8f20a9254a/width=512/img.png",
                    ConnectedModelInfo = new ConnectedModelInfo
                    {
                        ModelName = "Background Arts",
                        VersionName = "Anime Style v10",
                    },
                },
            ],
        };

    /// <inheritdoc />
    public IReadOnlySet<string> ModelIndexBlake3Hashes =>
        ModelIndex.Values.SelectMany(x => x).Select(x => x.HashBlake3).WhereNotNull().ToHashSet();

    /// <inheritdoc />
    public Task RefreshIndex()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IEnumerable<LocalModelFile> FindByModelType(SharedFolderType types)
    {
        return ModelIndex.Where(kvp => (kvp.Key & types) != 0).SelectMany(kvp => kvp.Value);
    }

    /// <inheritdoc />
    public Task<Dictionary<SharedFolderType, LocalModelFolder>> FindAllFolders()
    {
        return Task.FromResult(new Dictionary<SharedFolderType, LocalModelFolder>());
    }

    /// <inheritdoc />
    public Task<IEnumerable<LocalModelFile>> FindByModelTypeAsync(SharedFolderType type)
    {
        return Task.FromResult(Enumerable.Empty<LocalModelFile>());
    }

    /// <inheritdoc />
    public Task<IEnumerable<LocalModelFile>> FindByHashAsync(string hashBlake3)
    {
        return Task.FromResult(Enumerable.Empty<LocalModelFile>());
    }

    public Task<IEnumerable<LocalModelFile>> FindBySha256Async(string hashSha256)
    {
        return Task.FromResult(Enumerable.Empty<LocalModelFile>());
    }

    /// <inheritdoc />
    public Task<bool> RemoveModelAsync(LocalModelFile model)
    {
        return Task.FromResult(false);
    }

    public Task<bool> RemoveModelsAsync(IEnumerable<LocalModelFile> models)
    {
        return Task.FromResult(false);
    }

    public Task CheckModelsForUpdateAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void BackgroundRefreshIndex() { }
}
