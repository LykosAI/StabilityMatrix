namespace StabilityMatrix.Core.Python;

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
    /// Run a Function with PyRunning lock as a Task with GIL.
    /// </summary>
    /// <param name="func">Function to run.</param>
    /// <param name="waitTimeout">Time limit for waiting on PyRunning lock.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <exception cref="OperationCanceledException">cancelToken was canceled, or waitTimeout expired.</exception>
    Task<T> RunInThreadWithLock<T>(Func<T> func, TimeSpan? waitTimeout = null,
        CancellationToken cancelToken = default);

    /// <summary>
    /// Run an Action with PyRunning lock as a Task with GIL.
    /// </summary>
    /// <param name="action">Action to run.</param>
    /// <param name="waitTimeout">Time limit for waiting on PyRunning lock.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <exception cref="OperationCanceledException">cancelToken was canceled, or waitTimeout expired.</exception>
    Task RunInThreadWithLock(Action action, TimeSpan? waitTimeout = null,
        CancellationToken cancelToken = default);

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
