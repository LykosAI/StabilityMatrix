using System.Linq;
using System.Threading.Tasks;
using CefSharp;
using CommunityToolkit.Mvvm.ComponentModel;
using NLog;

namespace StabilityMatrix.ViewModels;

public partial class WebLoginViewModel : ObservableObject
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public void OnLoaded()
    {
    }

    public async Task OnRefresh()
    {
        var result = await IsUserLoggedIn();
    }
    
    private async Task<bool> IsUserLoggedIn()
    {
        var cookieVisitor = new TaskCookieVisitor();
        Cef.GetGlobalCookieManager().VisitUrlCookies("https://www.patreon.com", true, cookieVisitor);
        var cookies = await cookieVisitor.Task;
        Logger.Debug($"Found {cookies.Count} cookies");
        // Print names
        foreach (var cookie in cookies)
        {
            Logger.Debug($"Cookie ({cookie.Name}={cookie.Value})");
        }
        return cookies.Any(cookie => cookie.Name == "session_id");
    }
}
