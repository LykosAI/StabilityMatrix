using System.Text.Json;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Core.Services;

[RegisterSingleton<IModelCategoryService, ModelCategoryService>]
public class ModelCategoryService(
    ISettingsManager settingsManager,
    IModelIndexService modelIndexService,
    ILogger<ModelCategoryService> logger
) : IModelCategoryService
{
    private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

    /// <inheritdoc />
    public IReadOnlyList<ModelCategory> GetCategories()
    {
        var registered = settingsManager.Settings.ModelCategories;
        var known = new HashSet<string>(registered.Select(c => c.Name), NameComparer);

        var unregistered = modelIndexService
            .ModelIndexCategories.Where(name => !known.Contains(name))
            .OrderBy(name => name, NameComparer)
            .Select(name => new ModelCategory { Name = name });

        return [.. registered, .. unregistered];
    }

    /// <inheritdoc />
    public bool AddCategory(string name, string? color = null)
    {
        name = NormalizeName(name);

        if (settingsManager.Settings.ModelCategories.Any(c => NameComparer.Equals(c.Name, name)))
        {
            return false;
        }

        settingsManager.Transaction(s =>
            s.ModelCategories.Add(new ModelCategory { Name = name, Color = color })
        );
        return true;
    }

    /// <inheritdoc />
    public void SetCategoryColor(string name, string? color)
    {
        name = NormalizeName(name);

        settingsManager.Transaction(s =>
        {
            var existing = s.ModelCategories.FirstOrDefault(c => NameComparer.Equals(c.Name, name));
            if (existing is null)
            {
                s.ModelCategories.Add(new ModelCategory { Name = name, Color = color });
            }
            else
            {
                existing.Color = color;
            }
        });
    }

    /// <inheritdoc />
    public async Task RenameCategoryAsync(string oldName, string newName)
    {
        oldName = NormalizeName(oldName);
        newName = NormalizeName(newName);

        // A case-only change is still a rename; only an identical name is a no-op.
        if (oldName == newName)
        {
            return;
        }

        settingsManager.Transaction(s =>
        {
            var target = s.ModelCategories.FirstOrDefault(c => NameComparer.Equals(c.Name, newName));
            var source = s.ModelCategories.FirstOrDefault(c => NameComparer.Equals(c.Name, oldName));

            if (source is null)
            {
                return;
            }

            // Renaming onto an existing category merges into it.
            if (target is not null && !ReferenceEquals(target, source))
            {
                s.ModelCategories.Remove(source);
            }
            else
            {
                source.Name = newName;
            }
        });

        await RewriteMembershipsAsync(
                model => model.Categories.Contains(oldName, NameComparer),
                categories => ReplaceName(categories, oldName, newName)
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteCategoryAsync(string name)
    {
        name = NormalizeName(name);

        settingsManager.Transaction(s => s.ModelCategories.RemoveAll(c => NameComparer.Equals(c.Name, name)));

        await RewriteMembershipsAsync(
                model => model.Categories.Contains(name, NameComparer),
                categories => categories.Where(c => !NameComparer.Equals(c, name)).ToList()
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task SetCategoriesAsync(LocalModelFile model, IEnumerable<string> categories)
    {
        var normalized = categories.Select(NormalizeName).Distinct(NameComparer).ToList();
        return WriteMembershipsAsync([(model, normalized)]);
    }

    /// <inheritdoc />
    public Task AddToCategoryAsync(IEnumerable<LocalModelFile> models, string category)
    {
        category = NormalizeName(category);

        var changes = models
            .Where(m => !m.Categories.Contains(category, NameComparer))
            .Select(m => (m, (List<string>)[.. m.Categories, category]))
            .ToList();

        return WriteMembershipsAsync(changes);
    }

    /// <inheritdoc />
    public Task RemoveFromCategoryAsync(IEnumerable<LocalModelFile> models, string category)
    {
        category = NormalizeName(category);

        var changes = models
            .Where(m => m.Categories.Contains(category, NameComparer))
            .Select(m => (m, m.Categories.Where(c => !NameComparer.Equals(c, category)).ToList()))
            .ToList();

        return WriteMembershipsAsync(changes);
    }

    private Task RewriteMembershipsAsync(
        Func<LocalModelFile, bool> affects,
        Func<IReadOnlyList<string>, List<string>> transform
    )
    {
        var changes = modelIndexService
            .ModelIndex.Values.SelectMany(models => models)
            .Where(affects)
            .Select(m => (m, transform(m.Categories)))
            .ToList();

        return WriteMembershipsAsync(changes);
    }

    /// <summary>
    /// Load-update-save on each model's cm-info: the User block is replaced, everything else is
    /// carried across untouched. A sidecar left with nothing worth keeping is deleted. The
    /// updated models are then upserted into the index in one batch.
    /// </summary>
    private async Task WriteMembershipsAsync(
        IReadOnlyList<(LocalModelFile Model, List<string> Categories)> changes
    )
    {
        if (changes.Count == 0)
        {
            return;
        }

        var modelsRoot = settingsManager.ModelsDirectory;
        var updated = new List<LocalModelFile>(changes.Count);

        foreach (var (model, categories) in changes)
        {
            // Work on a copy: the index's own instance must stay untouched so the change is
            // visible to consumers diffing old against new.
            var cmInfo = CopyOrCreate(model.ConnectedModelInfo);
            var user = cmInfo.User?.Clone() ?? new UserModelMetadata();
            user.Categories = categories;
            cmInfo.User = user;

            var sidecarPath = model.GetConnectedModelInfoFullPath(modelsRoot);

            try
            {
                if (cmInfo.IsEmpty)
                {
                    if (File.Exists(sidecarPath))
                    {
                        File.Delete(sidecarPath);
                    }
                }
                else
                {
                    var directory = Path.GetDirectoryName(sidecarPath) ?? modelsRoot;
                    await cmInfo
                        .SaveJsonToDirectory(directory, model.FileNameWithoutExtension)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(e, "Failed to write categories for {Path}", model.RelativePath);
                continue;
            }

            updated.Add(model with { ConnectedModelInfo = cmInfo.IsEmpty ? null : cmInfo });
        }

        await modelIndexService.UpsertModelsAsync(updated).ConfigureAwait(false);
    }

    private static ConnectedModelInfo CopyOrCreate(ConnectedModelInfo? existing)
    {
        if (existing is null)
        {
            // Source defaults to CivitAI; a category-only sidecar has no source at all
            return new ConnectedModelInfo { Source = null };
        }

        return ConnectedModelInfo.FromJson(JsonSerializer.Serialize(existing)) ?? new ConnectedModelInfo();
    }

    private static List<string> ReplaceName(IReadOnlyList<string> categories, string oldName, string newName)
    {
        var result = new List<string>(categories.Count);
        foreach (var category in categories)
        {
            var mapped = NameComparer.Equals(category, oldName) ? newName : category;
            if (!result.Contains(mapped, NameComparer))
            {
                result.Add(mapped);
            }
        }
        return result;
    }

    private static string NormalizeName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Category name must not be empty", nameof(name));
        }
        return trimmed;
    }
}
