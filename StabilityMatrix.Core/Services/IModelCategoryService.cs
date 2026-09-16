using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Core.Services;

/// <summary>
/// Manages user-defined model categories: the registry of category names and colours in settings,
/// and which local models belong to them (persisted per model as a <c>.cm-user.json</c> sidecar).
/// Every membership change writes the sidecar and updates the model index, so consumers see it
/// through the usual <see cref="IModelIndexService"/> change notification.
/// </summary>
public interface IModelCategoryService
{
    /// <summary>
    /// All known categories: the registered ones from settings, followed by any names found on
    /// indexed models that are not registered (e.g. sidecars copied from another machine).
    /// </summary>
    IReadOnlyList<ModelCategory> GetCategories();

    /// <summary>
    /// Registers a category. Returns false if a category with that name (case-insensitive) already exists.
    /// </summary>
    bool AddCategory(string name, string? color = null);

    /// <summary>
    /// Sets the colour of a registered category, registering it first if needed.
    /// </summary>
    void SetCategoryColor(string name, string? color);

    /// <summary>
    /// Renames a category in the registry and on every indexed model that carries it.
    /// </summary>
    Task RenameCategoryAsync(string oldName, string newName);

    /// <summary>
    /// Removes a category from the registry and from every indexed model that carries it.
    /// </summary>
    Task DeleteCategoryAsync(string name);

    /// <summary>
    /// Replaces the category list of a single model.
    /// </summary>
    Task SetCategoriesAsync(LocalModelFile model, IEnumerable<string> categories);

    /// <summary>
    /// Adds a category to each of the given models that does not already have it.
    /// </summary>
    Task AddToCategoryAsync(IEnumerable<LocalModelFile> models, string category);

    /// <summary>
    /// Removes a category from each of the given models that has it.
    /// </summary>
    Task RemoveFromCategoryAsync(IEnumerable<LocalModelFile> models, string category);
}
