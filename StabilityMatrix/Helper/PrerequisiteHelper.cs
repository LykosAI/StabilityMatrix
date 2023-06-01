using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StabilityMatrix.Helper;

public class PrerequisiteHelper : IPrerequisiteHelper
{
    private readonly ILogger<PrerequisiteHelper> logger;

    public PrerequisiteHelper(ILogger<PrerequisiteHelper> logger)
    {
        this.logger = logger;
    }
    
    public async Task<Process?> InstallGitIfNecessary()
    {
        try
        {
            var gitOutput = await ProcessRunner.GetProcessOutputAsync("git", "--version");
            if (gitOutput.Contains("git version 2"))
            {
                return default;
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error running git: ");
        }

        var installProcess =
            ProcessRunner.StartProcess("Assets\\Git-2.40.1-64-bit.exe", "/VERYSILENT /NORESTART");
        installProcess.OutputDataReceived += (sender, args) => { logger.LogDebug(args.Data); };

        return installProcess;
    }
}
