using System.Threading.Tasks;

namespace StabilityMatrix.Avalonia.Services;

public interface IAccountsService
{
    bool IsLykosConnected { get; }

    Task LykosLoginAsync(string email, string password);

    Task LykosLogoutAsync();

    Task RefreshAsync();
}
