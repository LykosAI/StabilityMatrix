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

    Task LykosSignupAsync(string email, string password, string username);

    Task LykosLoginAsync(string email, string password);

    Task LykosLoginViaGoogleOAuthAsync(string code, string state, string codeVerifier);

    Task LykosLogoutAsync();

    Task LykosPatreonOAuthLogoutAsync();

    Task CivitLoginAsync(string apiToken);

    Task CivitLogoutAsync();

    Task RefreshAsync();
}
