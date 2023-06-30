using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace StabilityMatrix.Helper;

public static class ScreenExtensions
{
    public const string User32 = "user32.dll";
    public const string Shcore = "Shcore.dll";

    public static void GetDpi(this System.Windows.Forms.Screen screen, DpiType dpiType,
        out uint dpiX, out uint dpiY)
    {
        var pnt = new System.Drawing.Point(screen.Bounds.Left + 1, screen.Bounds.Top + 1);
        var mon = MonitorFromPoint(pnt, 2 /*MONITOR_DEFAULTTONEAREST*/);
        GetDpiForMonitor(mon, dpiType, out dpiX, out dpiY);
    }

    public static double GetScalingForPoint(System.Drawing.Point aPoint)
    {
        var mon = MonitorFromPoint(aPoint, 2 /*MONITOR_DEFAULTTONEAREST*/);
        uint dpiX, dpiY;
        GetDpiForMonitor(mon, DpiType.Effective, out dpiX, out dpiY);
        return (double) dpiX / 96.0;
    }


    [DllImport(User32)]
    private static extern IntPtr MonitorFromPoint([In] System.Drawing.Point pt, [In] uint dwFlags);


    [DllImport(Shcore)]
    private static extern IntPtr GetDpiForMonitor([In] IntPtr hmonitor, [In] DpiType dpiType,
        [Out] out uint dpiX, [Out] out uint dpiY);

    [DllImport(User32, CharSet = CharSet.Auto)]
    [ResourceExposure(ResourceScope.None)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WindowPlacement lpwndpl);

    [DllImport(User32, CharSet = CharSet.Auto, SetLastError = true)]
    [ResourceExposure(ResourceScope.None)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WindowPlacement lpwndpl);

    public enum DpiType
    {
        Effective = 0,
        Angular = 1,
        Raw = 2,
    }

    public static WindowPlacement GetPlacement(IntPtr hWnd)
    {
        var placement = new WindowPlacement();
        placement.length = Marshal.SizeOf(placement);
        GetWindowPlacement(hWnd, ref placement);
        return placement;
    }

    public static bool SetPlacement(IntPtr hWnd, WindowPlacement aPlacement)
    {
        var erg = SetWindowPlacement(hWnd, ref aPlacement);
        return erg;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Pointstruct
    {
        public int x;
        public int y;

        public Pointstruct(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;

        public Rect(int left, int top, int right, int bottom)
        {
            this.left = left;
            this.top = top;
            this.right = right;
            this.bottom = bottom;
        }

        public Rect(System.Windows.Rect r)
        {
            left = (int) r.Left;
            top = (int) r.Top;
            right = (int) r.Right;
            bottom = (int) r.Bottom;
        }

        public static Rect FromXywh(int x, int y, int width, int height)
        {
            return new Rect(x, y, x + width, y + height);
        }

        public System.Windows.Size Size => new(right - left, bottom - top);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WindowPlacement
    {
        public int length;
        public uint flags;
        public uint showCmd;
        public Pointstruct ptMinPosition;
        public Pointstruct ptMaxPosition;
        public Rect rcNormalPosition;

        public override string ToString()
        {
            var structBytes = RawSerialize(this);
            return Convert.ToBase64String(structBytes);
        }

        public void ReadFromBase64String(string aB64)
        {
            var b64 = Convert.FromBase64String(aB64);
            var newWp = ReadStruct<WindowPlacement>(b64, 0);
            length = newWp.length;
            flags = newWp.flags;
            showCmd = newWp.showCmd;
            ptMinPosition.x = newWp.ptMinPosition.x;
            ptMinPosition.y = newWp.ptMinPosition.y;
            ptMaxPosition.x = newWp.ptMaxPosition.x;
            ptMaxPosition.y = newWp.ptMaxPosition.y;
            rcNormalPosition.left = newWp.rcNormalPosition.left;
            rcNormalPosition.top = newWp.rcNormalPosition.top;
            rcNormalPosition.right = newWp.rcNormalPosition.right;
            rcNormalPosition.bottom = newWp.rcNormalPosition.bottom;
        }

        public static T ReadStruct<T>(byte[] aSrcBuffer, int aOffset)
        {
            var buffer = new byte[Marshal.SizeOf(typeof(T))];
            Buffer.BlockCopy(aSrcBuffer, aOffset, buffer, 0, Marshal.SizeOf(typeof(T)));
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            var temp = (T) Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return temp;
        }

        public static T ReadStruct<T>(Stream fs)
        {
            var buffer = new byte[Marshal.SizeOf(typeof(T))];
            fs.Read(buffer, 0, Marshal.SizeOf(typeof(T)));
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            var temp = (T) Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return temp;
        }

        public static byte[] RawSerialize(object anything)
        {
            var rawsize = Marshal.SizeOf(anything);
            var rawdata = new byte[rawsize];
            var handle = GCHandle.Alloc(rawdata, GCHandleType.Pinned);
            Marshal.StructureToPtr(anything, handle.AddrOfPinnedObject(), false);
            handle.Free();
            return rawdata;
        }
    }
}
