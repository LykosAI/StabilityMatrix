using NLog;
using StabilityMatrix.Avalonia.Logging;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class LogFileTargetTests
{
    [DataTestMethod]
    [DataRow("home")]
    [DataRow("${shortdate}")]
    [DataRow("${x}")]
    [DataRow(@"home\name")]
    public void Create_FileNamesUseLiteralLogDirectory(string homeDirName)
    {
        var logDir = new DirectoryPath(Path.GetTempPath(), homeDirName, "Logs");

        var target = LogFileTarget.Create(logDir);
        var logEvent = LogEventInfo.CreateNullEvent();

        Assert.AreEqual($"{logDir.FullPath}/app.log", target.FileName.Render(logEvent));
        Assert.AreEqual($"{logDir.FullPath}/app.{{#}}.log", target.ArchiveFileName.Render(logEvent));
    }
}
