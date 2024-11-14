using System;
using System.Linq;
using Nito.Disposables.Internals;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Api.OpenModelsDb;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;

public partial class OpenModelDbBrowserCardViewModel : DisposableViewModelBase
{
    private static Uri UsersBaseUri => new("https://openmodeldb.info/users");
    private static Uri ModelsBaseUri => new("https://openmodeldb.info/models");

    public OpenModelDbKeyedModel? Model { get; set; }

    public Uri? ModelUri => Model is { } model ? ModelsBaseUri.Append(model.Id) : null;

    public Uri? ThumbnailUri =>
        Model?.Thumbnail?.GetThumbnailAbsoluteUri()
        ?? Model?.Images?.Select(image => image.GetThumbnailAbsoluteUri()).WhereNotNull().FirstOrDefault();

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

    public Uri? DefaultAuthorProfileUri => DefaultAuthor is { } author ? UsersBaseUri.Append(author) : null;
}
