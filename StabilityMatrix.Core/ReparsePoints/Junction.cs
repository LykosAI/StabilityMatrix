using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace StabilityMatrix.Core.ReparsePoints;

[SupportedOSPlatform("windows")]
public static class Junction
{
    /// <summary>
    /// This prefix indicates to NTFS that the path is to be treated as a non-interpreted
    /// path in the virtual file system.
    /// </summary>
    private const string NonInterpretedPathPrefix = @"\??\";

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFile(
        [MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
        [MarshalAs(UnmanagedType.U4)] Win32FileAccess dwDesiredAccess,
        [MarshalAs(UnmanagedType.U4)] Win32FileShare dwShareMode,
        IntPtr lpSecurityAttributes,
        [MarshalAs(UnmanagedType.U4)] Win32CreationDisposition dwCreationDisposition,
        [MarshalAs(UnmanagedType.U4)] Win32FileAttribute dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode,
        [In] IntPtr lpInBuffer, uint nInBufferSize,
        IntPtr lpOutBuffer, uint nOutBufferSize,
        [Out] out uint lpBytesReturned, IntPtr lpOverlapped);
    
    /// <summary>
    /// Creates a junction point from the specified directory to the specified target directory.
    /// </summary>
    /// <param name="junctionPoint">The junction point path</param>
    /// <param name="targetDir">The target directory (Must already exist)</param>
    /// <param name="overwrite">If true overwrites an existing reparse point or empty directory</param>
    /// <exception cref="IOException">Thrown when the junction point could not be created or when
    /// an existing directory was found and <paramref name="overwrite" /> if false</exception>
    public static void Create(string junctionPoint, string targetDir, bool overwrite)
    {
        targetDir = Path.GetFullPath(targetDir);

        if (!Directory.Exists(targetDir))
        {
            throw new IOException("Target path does not exist or is not a directory");
        }

        if (Directory.Exists(junctionPoint))
        {
            if (!overwrite)
                throw new IOException("Directory already exists and overwrite parameter is false.");
        }
        else
        {
            Directory.CreateDirectory(junctionPoint);
        }

        using var fileHandle = OpenReparsePoint(junctionPoint, Win32FileAccess.GenericWrite);
        var targetDirBytes = Encoding.Unicode.GetBytes(
            NonInterpretedPathPrefix + Path.GetFullPath(targetDir));

        var reparseDataBuffer = new ReparseDataBuffer
        {
            ReparseTag = (uint) DeviceIoControlCode.ReparseTagMountPoint,
            ReparseDataLength = Convert.ToUInt16(targetDirBytes.Length + 12),
            SubstituteNameOffset = 0,
            SubstituteNameLength = Convert.ToUInt16(targetDirBytes.Length),
            PrintNameOffset = Convert.ToUInt16(targetDirBytes.Length + 2),
            PrintNameLength = 0,
            PathBuffer = new byte[0x3ff0]
        };

        Array.Copy(targetDirBytes, reparseDataBuffer.PathBuffer, targetDirBytes.Length);

        var inBufferSize = Marshal.SizeOf(reparseDataBuffer);
        var inBuffer = Marshal.AllocHGlobal(inBufferSize);

        try
        {
            Marshal.StructureToPtr(reparseDataBuffer, inBuffer, false);
            
            var result = DeviceIoControl(
                fileHandle, (uint) DeviceIoControlCode.SetReparsePoint,
                inBuffer, Convert.ToUInt32(targetDirBytes.Length + 20), 
                IntPtr.Zero, 0, 
                out var bytesReturned, IntPtr.Zero);
            
            Debug.WriteLine($"bytesReturned: {bytesReturned}");

            if (!result)
            {
                ThrowLastWin32Error($"Unable to create junction point" +
                                    $" {junctionPoint} -> {targetDir}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(inBuffer);
        }
    }
    
    /// <summary>
    /// Deletes a junction point at the specified source directory along with the directory itself.
    /// Does nothing if the junction point does not exist.
    /// </summary>
    /// <param name="junctionPoint">The junction point path</param>
    public static void Delete(string junctionPoint)
    {
        if (!Directory.Exists(junctionPoint))
        {
            if (File.Exists(junctionPoint))
                throw new IOException("Path is not a junction point.");

            return;
        }

        using var fileHandle = OpenReparsePoint(junctionPoint, Win32FileAccess.GenericWrite);
        
        var reparseDataBuffer = new ReparseDataBuffer
        {
            ReparseTag = (uint) DeviceIoControlCode.ReparseTagMountPoint,
            ReparseDataLength = 0,
            PathBuffer = new byte[0x3ff0]
        };

        var inBufferSize = Marshal.SizeOf(reparseDataBuffer);
        var inBuffer = Marshal.AllocHGlobal(inBufferSize);
        try
        {
            Marshal.StructureToPtr(reparseDataBuffer, inBuffer, false);
                
            var result = DeviceIoControl(fileHandle, 
                (uint) DeviceIoControlCode.DeleteReparsePoint,
                inBuffer, 8, 
                IntPtr.Zero, 0, 
                out var bytesReturned, IntPtr.Zero);

            Debug.WriteLine($"bytesReturned: {bytesReturned}");
            
            if (!result)
            {
                ThrowLastWin32Error($"Unable to delete junction point {junctionPoint}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(inBuffer);
        }

        try
        {
            Directory.Delete(junctionPoint);
        }
        catch (IOException ex)
        {
            throw new IOException("Unable to delete junction point.", ex);
        }
    }
    
    /// <summary>
    /// Determines whether the specified path exists and refers to a junction point.
    /// </summary>
    /// <param name="path">The junction point path</param>
    /// <returns>True if the specified path represents a junction point</returns>
    /// <exception cref="IOException">Thrown if the specified path is invalid
    /// or some other error occurs</exception>
    public static bool Exists(string path)
    {
        if (!Directory.Exists(path)) return false;

        using var handle = OpenReparsePoint(path, Win32FileAccess.GenericRead);
        var target = InternalGetTarget(handle);
        return target != null;
    }

    /// <summary>
    /// Gets the target of the specified junction point.
    /// </summary>
    /// <param name="junctionPoint">The junction point path</param>
    /// <returns>The target of the junction point</returns>
    /// <exception cref="IOException">Thrown when the specified path does not
    /// exist, is invalid, is not a junction point, or some other error occurs</exception>
    public static string GetTarget(string junctionPoint)
    {
        using var handle = OpenReparsePoint(junctionPoint, Win32FileAccess.GenericRead);
        var target = InternalGetTarget(handle);
        if (target == null)
        {
            throw new IOException("Path is not a junction point.");
        }
        return target;
    }
    
    private static string? InternalGetTarget(SafeFileHandle handle)
    {
        var outBufferSize = Marshal.SizeOf(typeof(ReparseDataBuffer));
        var outBuffer = Marshal.AllocHGlobal(outBufferSize);

        try
        {
            var result = DeviceIoControl(
                handle, 
                (uint) DeviceIoControlCode.GetReparsePoint,
                IntPtr.Zero, 
                0, 
                outBuffer, 
                (uint) outBufferSize, 
                out var bytesReturned, 
                IntPtr.Zero);

            Debug.WriteLine($"bytesReturned: {bytesReturned}");
            
            // Errors
            if (!result)
            {
                var error = Marshal.GetLastWin32Error();
                if (error == (int) Win32ErrorCode.NotAReparsePoint)
                {
                    return null;
                }
                else
                {
                    ThrowLastWin32Error("Unable to get information about junction point.");
                }
            }

            // Check output
            if (outBuffer == IntPtr.Zero) return null;
            // Safe interpret as ReparseDataBuffer type
            if (Marshal.PtrToStructure(outBuffer, typeof(ReparseDataBuffer))
                is not ReparseDataBuffer reparseDataBuffer)
            {
                return null;
            }

            // Check if it's a mount point
            if (reparseDataBuffer.ReparseTag != (uint) DeviceIoControlCode.ReparseTagMountPoint)
            {
                return null;
            }

            // Get the target dir string
            var targetDir = Encoding.Unicode.GetString(reparseDataBuffer.PathBuffer,
                reparseDataBuffer.SubstituteNameOffset, reparseDataBuffer.SubstituteNameLength);

            if (targetDir.StartsWith(NonInterpretedPathPrefix))
            {
                targetDir = targetDir[NonInterpretedPathPrefix.Length..];
            }

            return targetDir;
        }
        finally
        {
            Marshal.FreeHGlobal(outBuffer);
        }
    }

    private static SafeFileHandle OpenReparsePoint(string reparsePoint, Win32FileAccess accessMode)
    {
        var filePtr = CreateFile(
            reparsePoint,
            accessMode,
            Win32FileShare.Read | Win32FileShare.Write | Win32FileShare.Delete,
            IntPtr.Zero,
            Win32CreationDisposition.OpenExisting,
            Win32FileAttribute.FlagBackupSemantics | Win32FileAttribute.FlagOpenReparsePoint,
            IntPtr.Zero);

        var handle = new SafeFileHandle(filePtr, true);

        if (Marshal.GetLastWin32Error() != 0)
        {
            ThrowLastWin32Error($"Unable to open reparse point {reparsePoint}");
        }
        
        return handle;
    }
    
    [DoesNotReturn]
    private static void ThrowLastWin32Error(string message)
    {
        throw new IOException(message, Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
    }
}
