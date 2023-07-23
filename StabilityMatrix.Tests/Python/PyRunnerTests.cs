using Python.Runtime;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Python;

[TestClass]
public class PyRunnerTests
{
    private static readonly PyRunner PyRunner = new();
    
    [ClassInitialize]
    public static async Task TestInitialize(TestContext testContext)
    {
        var settingsManager = new SettingsManager();
        if (!settingsManager.TryFindLibrary())
        {
            GlobalConfig.LibraryDir = GlobalConfig.HomeDir;
            PyRunner.HomeDir = GlobalConfig.HomeDir;
        }
        
        await PyRunner.Initialize();
    }
    
    [TestMethod]
    public void PythonEngine_ShouldBeInitialized()
    {
        Assert.IsTrue(PythonEngine.IsInitialized);
    }
    
    [TestMethod]
    public async Task RunEval_ShouldReturnOutput()
    {
        // Arrange
        const string script = "print('Hello World')";
        
        // Act
        var result = await PyRunner.Eval(script);
        var stdout = PyRunner.StdOutStream!.GetBuffer();
        
        // Assert
        Assert.AreEqual("Hello World\n", stdout);
        Assert.AreEqual("None", result);
    }
}
