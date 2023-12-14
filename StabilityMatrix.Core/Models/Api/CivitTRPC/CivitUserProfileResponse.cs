using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.CivitTRPC;

/*
 * Example:
 * {
    "result": {
        "data": {
            "json": {
                "id": 1020931,
                "username": "owo",
                "deletedAt": null,
                "image": "https://lh3.googleusercontent.com/a/...",
                "leaderboardShowcase": null,
                "createdAt": "2023-02-01T21:05:31.125Z",
                "cosmetics": [],
                "links": [],
                "rank": null,
                "stats": null,
                "profile": {
                    "bio": null,
                    "coverImageId": null,
                    "coverImage": null,
                    "message": null,
                    "messageAddedAt": null,
                    "profileSectionsSettings": [
                        {
                            "key": "showcase",
                            "enabled": true
                        },
                        {
                            "key": "popularModels",
                            "enabled": true
                        },
                        {
                            "key": "popularArticles",
                            "enabled": true
                        },
                        {
                            "key": "modelsOverview",
                            "enabled": true
                        },
                        {
                            "key": "imagesOverview",
                            "enabled": true
                        },
                        {
                            "key": "recentReviews",
                            "enabled": true
                        }
                    ],
                    "privacySettings": {
                        "showFollowerCount": true,
                        "showReviewsRating": true,
                        "showFollowingCount": true
                    },
                    "showcaseItems": [],
                    "location": null,
                    "nsfw": false,
                    "userId": 1020931
                }
            },
            "meta": {
                "values": {
                    "createdAt": [
                        "Date"
                    ]
                }
            }
        }
    }
}
 *
*/

public record CivitUserProfileResponse
{
    [JsonPropertyName("result")]
    public required JsonObject Result { get; init; }

    public int? UserId => Result["data"]?["json"]?["id"]?.GetValue<int>();

    public string? Username => Result["data"]?["json"]?["username"]?.GetValue<string>();

    public string? ImageUrl => Result["data"]?["json"]?["image"]?.GetValue<string>();

    public DateTimeOffset? CreatedAt =>
        Result["data"]?["json"]?["createdAt"]?.GetValue<DateTimeOffset>();
}
