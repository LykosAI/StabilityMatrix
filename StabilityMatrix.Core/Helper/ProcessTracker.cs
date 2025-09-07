using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;
using NLog;

namespace StabilityMatrix.Core.Helper;

/// <summary>
/// Allows processes to be automatically killed if this parent process unexpectedly quits.
/// This feature requires Windows 8 or greater. On Windows 7, nothing is done.</summary>
/// <remarks>References:
///  https://stackoverflow.com/a/4657392/386091
///  https://stackoverflow.com/a/9164742/386091 </remarks>
[SupportedOSPlatform("windows")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public static partial class ProcessTracker
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly Lazy<JobObject?> ProcessTrackerJobLazy = new(() =>
    {
        if (!JobObject.IsAvailableOnCurrentPlatform)
        {
            return null;
        }

        // The job name is optional (and can be null) but it helps with diagnostics.
        //  If it's not null, it has to be unique. Use SysInternals' Handle command-line
        //  utility: handle -a ChildProcessTracker
        var jobName = $"SM_ProcessTracker_{Environment.ProcessId}";

        Logger.Debug("Creating Job Object {Job}", jobName);

        try
        {
            return new JobObject(jobName);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to create Job Object, ProcessTracker will be unavailable");
            return null;
        }
    });

    private static JobObject? ProcessTrackerJob => ProcessTrackerJobLazy.Value;

    /// <summary>
    /// Add the process to be tracked. If our current process is killed, the child processes
    /// that we are tracking will be automatically killed, too. If the child process terminates
    /// first, that's fine, too.
    ///
    /// Ignored if the Process has already exited.
    /// </summary>
    public static void AddProcess(Process process)
    {
        // Skip if no job object
        if (ProcessTrackerJob is not { } job)
        {
            return;
        }

        // Skip if process already exited, this sometimes throws
        try
        {
            if (process.HasExited)
            {
                return;
            }
        }
        catch (InvalidOperationException)
        {
            return;
        }

        try
        {
            Logger.Debug(
                "Adding Process {Process} [{Id}] to Job Object {Job}",
                process.ProcessName,
                process.Id,
                job.Name
            );

            job.AssignProcess(process);
        }
        catch (Exception)
        {
            // Check again if the process has exited, if it hasn't, rethrow
            try
            {
                if (process.HasExited)
                {
                    return;
                }
            }
            catch (InvalidOperationException)
            {
                return;
            }

            throw;
        }
    }

    /// <summary>
    /// Add the process to be tracked in a new job. If our current process is killed, the child processes
    /// that we are tracking will be automatically killed, too. If the child process terminates
    /// first, that's fine, too.
    ///
    /// Ignored if the Process has already exited.
    /// </summary>
    public static void AttachExitHandlerJobToProcess(Process process)
    {
        // Skip if job object is not available
        if (!JobObject.IsAvailableOnCurrentPlatform)
        {
            return;
        }

        // Skip if process already exited, this sometimes throws
        try
        {
            if (process.HasExited)
            {
                return;
            }
        }
        catch (InvalidOperationException)
        {
            return;
        }

        // Create a new job object for this process
        var jobName = $"SM_ProcessTracker_{Environment.ProcessId}_Instance_{process.Id}";

        Logger.Debug("Creating Instance Job Object {Job}", jobName);

        var instanceJob = new JobObject(jobName);

        try
        {
            Logger.Debug(
                "Adding Process {Process} [{Id}] to Job Object {Job}",
                process.ProcessName,
                process.Id,
                jobName
            );

            instanceJob.AssignProcess(process);

            // Dispose the instance job when the process exits
            process.Exited += (_, _) =>
            {
                Logger.Debug(
                    "Process {Process} [{Id}] exited ({Code}), terminating instance Job Object {Job}",
                    process.ProcessName,
                    process.Id,
                    process.ExitCode,
                    jobName
                );

                // ReSharper disable twice AccessToDisposedClosure
                if (!instanceJob.IsClosed)
                {
                    // Convert from negative to two's complement if needed
                    var exitCode =
                        process.ExitCode < 0 ? (uint)(4294967296 + process.ExitCode) : (uint)process.ExitCode;

                    instanceJob.Terminate(exitCode);
                    instanceJob.Dispose();
                }
            };
        }
        catch (Exception)
        {
            instanceJob.Dispose();
            throw;
        }
    }

    private class JobObject : SafeHandleZeroOrMinusOneIsInvalid
    {
        // This feature requires Windows 8 or later
        public static bool IsAvailableOnCurrentPlatform =>
            Compat.IsWindows && Environment.OSVersion.Version >= new Version(6, 2);

        public string Name { get; }

        public JobObject(string name)
            : base(true)
        {
            if (!IsAvailableOnCurrentPlatform)
            {
                throw new PlatformNotSupportedException("This feature requires Windows 8 or later.");
            }

            Name = name;

            handle = CreateJobObject(IntPtr.Zero, name);

            var info = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                // This is the key flag. When our process is killed, Windows will automatically
                //  close the job handle, and when that happens, we want the child processes to
                //  be killed, too.
                LimitFlags = JOBOBJECTLIMIT.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
            };

            var length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION { BasicLimitInformation = info };
            var extendedInfoPtr = Marshal.AllocHGlobal(length);

            try
            {
                Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);

                if (
                    !SetInformationJobObject(
                        handle,
                        JobObjectInfoType.ExtendedLimitInformation,
                        extendedInfoPtr,
                        (uint)length
                    )
                )
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            finally
            {
                Marshal.FreeHGlobal(extendedInfoPtr);
            }
        }

        public void AssignProcess(Process process)
        {
            ObjectDisposedException.ThrowIf(handle == IntPtr.Zero, typeof(JobObject));

            if (!AssignProcessToJobObject(handle, process.Handle))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public void AssignProcess(IntPtr processHandle)
        {
            ObjectDisposedException.ThrowIf(handle == IntPtr.Zero, typeof(JobObject));

            if (!AssignProcessToJobObject(handle, processHandle))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public void Terminate(uint exitCode)
        {
            ObjectDisposedException.ThrowIf(handle == IntPtr.Zero, typeof(JobObject));

            if (!TerminateJobObject(handle, exitCode))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        protected override bool ReleaseHandle()
        {
            return CloseHandle(handle);
        }
    }

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "CreateJobObjectW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16
    )]
    private static partial IntPtr CreateJobObject(IntPtr lpJobAttributes, string name);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetInformationJobObject(
        IntPtr job,
        JobObjectInfoType infoType,
        IntPtr lpJobObjectInfo,
        uint cbJobObjectInfoLength
    );

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr hObject);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TerminateJobObject(IntPtr job, uint exitCode);

    internal enum JobObjectInfoType
    {
        AssociateCompletionPortInformation = 7,
        BasicLimitInformation = 2,
        BasicUIRestrictions = 4,
        EndOfJobTimeInformation = 6,
        ExtendedLimitInformation = 9,
        SecurityLimitInformation = 5,
        GroupInformation = 11,
    }

    [StructLayout(LayoutKind.Sequential)]
    // ReSharper disable once IdentifierTypo
    internal struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public Int64 PerProcessUserTimeLimit;
        public Int64 PerJobUserTimeLimit;
        public JOBOBJECTLIMIT LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public UInt32 ActiveProcessLimit;
        public Int64 Affinity;
        public UInt32 PriorityClass;
        public UInt32 SchedulingClass;
    }

    [Flags]
    // ReSharper disable once IdentifierTypo
    internal enum JOBOBJECTLIMIT : uint
    {
        JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct IO_COUNTERS
    {
        public UInt64 ReadOperationCount;
        public UInt64 WriteOperationCount;
        public UInt64 OtherOperationCount;
        public UInt64 ReadTransferCount;
        public UInt64 WriteTransferCount;
        public UInt64 OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    // ReSharper disable once IdentifierTypo
    internal struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }
}
