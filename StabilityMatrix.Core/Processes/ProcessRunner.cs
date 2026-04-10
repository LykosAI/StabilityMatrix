using System.Diagnostics;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class ProcessRunnerTests
{
    [TestMethod]
    public void PrepareEnvironment_SanitizesInheritedPythonVariables_ForPythonProcesses()
    {
        var info = new ProcessStartInfo();
        info.Environment["PYTHONHOME"] = @"C:\host\python";
        info.Environment["PYTHONPATH"] = @"C:\host\packages";
        info.Environment["VIRTUAL_ENV"] = @"C:\host\venv";
        info.Environment["CONDA_PREFIX"] = @"C:\host\conda";

        ProcessRunner.PrepareEnvironment(info, "python.exe", null);

        Assert.IsFalse(info.Environment.ContainsKey("PYTHONHOME"));
        Assert.IsFalse(info.Environment.ContainsKey("PYTHONPATH"));
        Assert.IsFalse(info.Environment.ContainsKey("VIRTUAL_ENV"));
        Assert.IsFalse(info.Environment.ContainsKey("CONDA_PREFIX"));
        Assert.AreEqual("1", info.Environment["PYTHONNOUSERSITE"]);
    }

    [TestMethod]
    public void PrepareEnvironment_PreservesExplicitPythonOverrides()
    {
        var info = new ProcessStartInfo();
        info.Environment["PYTHONHOME"] = @"C:\host\python";
        info.Environment["PYTHONPATH"] = @"C:\host\packages";
        info.Environment["VIRTUAL_ENV"] = @"C:\host\venv";
        info.Environment["PYTHONNOUSERSITE"] = "1";

        var explicitEnvironment = new Dictionary<string, string>
        {
            ["PYTHONPATH"] = @"C:\package\pythonpath",
            ["VIRTUAL_ENV"] = @"C:\package\venv",
            ["PYTHONNOUSERSITE"] = "0",
        };

        ProcessRunner.PrepareEnvironment(info, "uv.exe", explicitEnvironment);

        Assert.IsFalse(info.Environment.ContainsKey("PYTHONHOME"));
        Assert.AreEqual(@"C:\package\pythonpath", info.Environment["PYTHONPATH"]);
        Assert.AreEqual(@"C:\package\venv", info.Environment["VIRTUAL_ENV"]);
        Assert.AreEqual("0", info.Environment["PYTHONNOUSERSITE"]);
    }

    [TestMethod]
    public void PrepareEnvironment_LeavesNonPythonProcessesUntouched()
    {
        var info = new ProcessStartInfo();
        info.Environment["PYTHONHOME"] = @"C:\host\python";

        ProcessRunner.PrepareEnvironment(info, "git.exe", null);

        Assert.AreEqual(@"C:\host\python", info.Environment["PYTHONHOME"]);
        Assert.IsFalse(info.Environment.ContainsKey("PYTHONNOUSERSITE"));
    }

    [TestMethod]
    public void SanitizeCurrentProcessPythonEnvironment_RemovesInheritedValues()
    {
        const string pythonHome = @"C:\host\python";
        const string pythonPath = @"C:\host\packages";

        var originalPythonHome = Environment.GetEnvironmentVariable(
            "PYTHONHOME",
            EnvironmentVariableTarget.Process
        );
        var originalPythonPath = Environment.GetEnvironmentVariable(
            "PYTHONPATH",
            EnvironmentVariableTarget.Process
        );
        var originalPythonNoUserSite = Environment.GetEnvironmentVariable(
            "PYTHONNOUSERSITE",
            EnvironmentVariableTarget.Process
        );

        try
        {
            Environment.SetEnvironmentVariable("PYTHONHOME", pythonHome, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("PYTHONPATH", pythonPath, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("PYTHONNOUSERSITE", null, EnvironmentVariableTarget.Process);

            ProcessRunner.SanitizeCurrentProcessPythonEnvironment();

            Assert.IsNull(
                Environment.GetEnvironmentVariable("PYTHONHOME", EnvironmentVariableTarget.Process)
            );
            Assert.IsNull(
                Environment.GetEnvironmentVariable("PYTHONPATH", EnvironmentVariableTarget.Process)
            );
            Assert.AreEqual(
                "1",
                Environment.GetEnvironmentVariable("PYTHONNOUSERSITE", EnvironmentVariableTarget.Process)
            );
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "PYTHONHOME",
                originalPythonHome,
                EnvironmentVariableTarget.Process
            );
            Environment.SetEnvironmentVariable(
                "PYTHONPATH",
                originalPythonPath,
                EnvironmentVariableTarget.Process
            );
            Environment.SetEnvironmentVariable(
                "PYTHONNOUSERSITE",
                originalPythonNoUserSite,
                EnvironmentVariableTarget.Process
            );
        }
    }
}
