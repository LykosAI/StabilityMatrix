using System.IO;
using System.Threading.Tasks;

namespace StabilityMatrix;

public interface IPyRunner
{
    /// <summary>
    /// Initializes the Python runtime using the embedded dll.
    /// Can be called with no effect after initialization.
    /// </summary>
    /// <exception cref="FileNotFoundException">Thrown if Python DLL not found.</exception>
    Task Initialize();

    /// <summary>
    /// One-time setup for get-pip
    /// </summary>
    Task SetupPip();

    /// <summary>
    /// Install a Python package with pip
    /// </summary>
    Task InstallPackage(string package);

    /// <summary>
    /// Evaluate Python expression and return its value as a string
    /// </summary>
    /// <param name="expression"></param>
    Task<string> Eval(string expression);

    /// <summary>
    /// Evaluate Python expression and return its value
    /// </summary>
    /// <param name="expression"></param>
    Task<T> Eval<T>(string expression);

    /// <summary>
    /// Execute Python code without returning a value
    /// </summary>
    /// <param name="code"></param>
    Task Exec(string code);

    /// <summary>
    /// Return the Python version as a PyVersionInfo struct
    /// </summary>
    Task<PyVersionInfo> GetVersionInfo();
}