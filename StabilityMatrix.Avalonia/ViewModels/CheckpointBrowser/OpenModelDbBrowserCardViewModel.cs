using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Nito.Disposables.Internals;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Api.OpenModelsDb;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;

[Localizable(false)]
public sealed class OpenModelDbBrowserCardViewModel(OpenModelDbManager openModelDbManager)
{
    public OpenModelDbKeyedModel? Model { get; set; }

    public Uri? ModelUri => Model is { } model ? openModelDbManager.ModelsBaseUri.Append(model.Id) : null;

    public Uri? ThumbnailUri =>
        Model?.Thumbnail?.GetImageAbsoluteUris().FirstOrDefault()
        ?? Model
            ?.Images
            ?.Select(image => image.GetImageAbsoluteUris().FirstOrDefault())
            .WhereNotNull()
            .FirstOrDefault();

    public IEnumerable<OpenModelDbTag> Tags =>
        Model?.Tags?.Select(tagId => openModelDbManager.Tags?.GetValueOrDefault(tagId)).WhereNotNull() ?? [];

    public OpenModelDbArchitecture? Architecture =>
        Model?.Architecture is { } architectureId
            ? openModelDbManager.Architectures?.GetValueOrDefault(architectureId)
            : null;

    public string? DisplayScale => Model?.Scale is { } scale ? $"{scale}x" : null;

    public string? DefaultAuthor
    {
        get
        {
            if (Model?.Author?.Value is string author)
            {
                return author;
            }
            if (Model?.Author?.Value is string[] { Length: > 0 } authorArray)
            {
                return authorArray.First();
            }
            return null;
        }
    }

    public Uri? DefaultAuthorProfileUri =>
        DefaultAuthor is { } author ? openModelDbManager.UsersBaseUri.Append(author) : null;
}
