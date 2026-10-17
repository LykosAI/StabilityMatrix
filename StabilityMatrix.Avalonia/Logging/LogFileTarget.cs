using NLog.Layouts;
using NLog.Targets;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Avalonia.Logging;

public static class LogFileTarget
{
    /// <summary>
    /// Creates the app's rolling file target, writing app.log and its archives under <paramref name="logDir"/>.
    /// </summary>
    public static FileTarget Create(DirectoryPath logDir)
    {
        // FileName and ArchiveFileName are NLog layouts, so escape the literal path
        // (e.g. a home directory named "${shortdate}" must not be rendered as a date)
        var escapedLogDir = SimpleLayout.Escape(logDir.FullPath);

        return new FileTarget("logfile")
        {
            Layout = "${longdate}|${level:uppercase=true}|${logger}|${message:withexception=true}",
            FileName = $"{escapedLogDir}/app.log",
            ArchiveOldFileOnStartup = true,
            ArchiveFileName = $"{escapedLogDir}/app.{{#}}.log",
            ArchiveDateFormat = "yyyy-MM-dd HH_mm_ss",
            ArchiveNumbering = ArchiveNumberingMode.Date,
            MaxArchiveFiles = 9,
        };
    }
}
