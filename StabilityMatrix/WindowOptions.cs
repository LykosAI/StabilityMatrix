using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace StabilityMatrix;

public static class WindowOptions
{
    public static void TrySetCustomTitle(Window window, UIElement titleBar)
    {
        // Use custom title bar if supported
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var appWindow = GetAppWindow(window);
            // Hide default title bar
            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            // Set new title bar
            window.SetTitleBar(titleBar);
        }
        else
        {
            // Customization is not supported, hide the custom title bar
            titleBar.Visibility = Visibility.Collapsed;
        }
    }

    private static AppWindow GetAppWindow(Window window)
    {
        var hWnd = WindowNative.GetWindowHandle(window);
        var wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
        return AppWindow.GetFromWindowId(wndId);
    }
}