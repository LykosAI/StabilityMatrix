using System.Web;

namespace StabilityMatrix.Core.Models.Api.Lykos;

public class GoogleOAuthResponse
{
    public string? Code { get; init; }

    public string? State { get; init; }

    public string? Nonce { get; init; }

    public string? Error { get; init; }

    public static GoogleOAuthResponse ParseFromQueryString(string query)
    {
        var queryCollection = HttpUtility.ParseQueryString(query);

        return new GoogleOAuthResponse
        {
            Code = queryCollection.Get("code"),
            State = queryCollection.Get("state"),
            Nonce = queryCollection.Get("nonce"),
            Error = queryCollection.Get("error")
        };
    }
}
