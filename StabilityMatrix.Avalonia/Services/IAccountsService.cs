using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Api.Lykos;

namespace StabilityMatrix.Avalonia.Services;

public interface IAccountsService
{
    event EventHandler<LykosAccountStatusUpdateEventArgs>? LykosAccountStatusUpdate;
    event EventHandler<CivitAccountStatusUpdateEventArgs>? CivitAccountStatusUpdate;
    event EventHandler<HuggingFaceAccountStatusUpdateEventArgs>? HuggingFaceAccountStatusUpdate;
    event EventHandler<GeminiAccountStatusUpdateEventArgs>? GeminiAccountStatusUpdate;

    LykosAccountStatusUpdateEventArgs? LykosStatus { get; }
    CivitAccountStatusUpdateEventArgs? CivitStatus { get; }
    HuggingFaceAccountStatusUpdateEventArgs? HuggingFaceStatus { get; }
    GeminiAccountStatusUpdateEventArgs? GeminiStatus { get; }

    /// <summary>
    /// Returns whether SecretsManager has a stored Lykos V2 account.
    /// Does not mean <see cref="LykosStatus"/> is populated or refresh/access tokens are valid.
    /// </summary>
    Task<bool> HasStoredLykosAccountAsync();

    [Obsolete]
    Task LykosSignupAsync(string email, string password, string username);

    [Obsolete]
    Task LykosLoginAsync(string email, string password);

    [Obsolete]
    Task LykosLoginViaGoogleOAuthAsync(string code, string state, string codeVerifier);

    [Obsolete]
    Task LykosLogoutAsync();

    Task LykosAccountV2LoginAsync(LykosAccountV2Tokens tokens);

    Task LykosAccountV2LogoutAsync();

    Task LykosPatreonOAuthLogoutAsync();

    Task CivitLoginAsync(string apiToken);

    Task CivitLogoutAsync();

    Task RefreshAsync();

    Task RefreshLykosAsync();

    Task HuggingFaceLoginAsync(string token);
    Task HuggingFaceLogoutAsync();
    Task RefreshHuggingFaceAsync();

    Task GeminiLoginAsync(string apiKey);
    Task GeminiLogoutAsync();
    Task RefreshGeminiAsync();
}
