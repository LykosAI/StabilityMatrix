using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.OpenModelsDb;

/// <summary>
/// Apparently all the urls can be either standalone or paired.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type", IgnoreUnrecognizedTypeDiscriminators = true)]
[JsonDerivedType(typeof(Paired), "paired")]
[JsonDerivedType(typeof(Standalone), "standalone")]
public class OpenModelDbImage
{
    /*{
        "type": "paired",
        "LR": "https://images2.imgbox.com/09/3f/ZcIq3bwn_o.jpeg",
        "SR": "https://images2.imgbox.com/c7/dd/lIHpU4PZ_o.png",
        "thumbnail": "/thumbs/small/6e36848722bccb84eca5232a.jpg"
    }*/
    /*{
        "type": "paired",
        "LR": "/thumbs/573c7a3162c9831716c3bb35.jpg",
        "SR": "/thumbs/9a7cb6631356006c30f177d1.jpg",
        "LRSize": {
            "width": 366,
            "height": 296
        },
        "SRSize": {
            "width": 366,
            "height": 296
        }
    }*/
    public class Paired : OpenModelDbImage
    {
        [JsonPropertyName("LR")]
        public Uri? Lr { get; set; }

        [JsonPropertyName("SR")]
        public Uri? Sr { get; set; }

        public Uri? Thumbnail { get; set; }

        public override Uri? GetThumbnailAbsoluteUri()
        {
            return ToAbsoluteUri(Sr) ?? ToAbsoluteUri(Lr) ?? ToAbsoluteUri(Thumbnail);
        }
    }

    /*
     {
        "type": "standalone",
        "url": "https://i.slow.pics/rE3PKKTD.webp",
        "thumbnail": "/thumbs/small/85e62ea0e6801e7a0bf5acb6.jpg"
    }
    */
    public class Standalone : OpenModelDbImage
    {
        public Uri? Url { get; set; }

        public Uri? Thumbnail { get; set; }

        public override Uri? GetThumbnailAbsoluteUri()
        {
            return ToAbsoluteUri(Url) ?? ToAbsoluteUri(Thumbnail);
        }
    }

    public static Uri? ToAbsoluteUri(Uri? url)
    {
        if (url is null)
        {
            return null;
        }

        if (url.IsAbsoluteUri)
        {
            return url;
        }

        var baseUri = new Uri("https://openmodeldb.info/");

        return new Uri(baseUri, url);
    }

    public virtual Uri? GetThumbnailAbsoluteUri()
    {
        return null;
    }
}
