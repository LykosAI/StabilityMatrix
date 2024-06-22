using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace StabilityMatrix.Avalonia.Helpers;

[SupportedOSPlatform("windows")]
public static class WindowsClipboard
{
    public static async Task SetBitmapAsync(Bitmap bitmap)
    {
        await Task.Run(() => SetBitmap(bitmap));
    }

    public static void SetBitmap(Bitmap bitmap)
    {
        if (bitmap == null)
            throw new ArgumentNullException(nameof(bitmap));

        // Convert from Avalonia Bitmap to System Bitmap
        var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream); // this returns a png from Skia (we could save/load it from the system bitmap to convert it to a bmp first, but this seems to work well already)

        var systemBitmap = new System.Drawing.Bitmap(memoryStream);

        var hBitmap = systemBitmap.GetHbitmap();

        var screenDC = GetDC(IntPtr.Zero);

        var sourceDC = CreateCompatibleDC(screenDC);
        var sourceBitmapSelection = SelectObject(sourceDC, hBitmap);

        var destDC = CreateCompatibleDC(screenDC);
        var compatibleBitmap = CreateCompatibleBitmap(screenDC, systemBitmap.Width, systemBitmap.Height);

        var destinationBitmapSelection = SelectObject(destDC, compatibleBitmap);

        BitBlt(destDC, 0, 0, systemBitmap.Width, systemBitmap.Height, sourceDC, 0, 0, 0x00CC0020); // SRCCOPY

        try
        {
            OpenClipboard(IntPtr.Zero);

            EmptyClipboard();

            var result = SetClipboardData((uint)Win32ClipboardFormat.CF_BITMAP, compatibleBitmap);

            if (result == IntPtr.Zero)
            {
                var errno = Marshal.GetLastWin32Error();
                throw new Win32Exception(errno, $"SetClipboardData failed");
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", ExactSpelling = true)]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    public static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);

    [DllImport("gdi32.dll", SetLastError = true, ExactSpelling = true)]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

    [DllImport("gdi32.dll", SetLastError = true, ExactSpelling = true)]
    public static extern bool BitBlt(
        IntPtr hdc,
        int x,
        int y,
        int cx,
        int cy,
        IntPtr hdcSrc,
        int x1,
        int y1,
        uint rop
    );
}
