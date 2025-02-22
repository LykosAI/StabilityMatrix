using System;
using System.Threading.Tasks;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Api.Lykos;

namespace StabilityMatrix.Avalonia.Services;

public interface IAccountsService
{
    event EventHandler<LykosAccountStatusUpdateEventArgs>? LykosAccountStatusUpdate;

    event EventHandler<CivitAccountStatusUpdateEventArgs>? CivitAccountStatusUpdate;

    LykosAccountStatusUpdateEventArgs? LykosStatus { get; }

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
}
