using System;
using System.Threading.Tasks;
using NLog;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Avalonia.Models.PackageSteps;

public class UnpackSiteCustomizeStep(DirectoryPath venvPath) : IPackageStep
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public async Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        var sitePackages = venvPath.JoinDir(PyVenvRunner.RelativeSitePackagesPath);
        var file = sitePackages.JoinFile("sitecustomize.py");
        file.Directory?.Create();

        // Only rewrite when missing or out of date. A mismatch can also mean the
        // on-disk copy was corrupted/truncated by external software (e.g. some
        // antivirus suites), which would otherwise break every interpreter call
        // since sitecustomize loads on startup.
        var expected = await Assets.PyScriptSiteCustomize.ReadAsStringAsync();
        if (!file.Exists || (await TryReadAsync(file)) != expected)
        {
            await Assets.PyScriptSiteCustomize.ExtractTo(file, true);
        }

        // Drop any stale/corrupt compiled bytecode so it's regenerated from the
        // freshly written source rather than loaded from a bad cache.
        ClearCompiledCache(sitePackages);
    }

    private static async Task<string?> TryReadAsync(FilePath file)
    {
        try
        {
            return await file.ReadAllTextAsync();
        }
        catch (Exception e)
        {
            Logger.Warn(e, "Failed to read existing sitecustomize.py, will rewrite");
            return null;
        }
    }

    private static void ClearCompiledCache(DirectoryPath sitePackages)
    {
        var pycache = sitePackages.JoinDir("__pycache__");
        if (!pycache.Exists)
            return;

        foreach (var cached in pycache.EnumerateFiles("sitecustomize.*.pyc"))
        {
            try
            {
                cached.Delete();
            }
            catch (Exception e)
            {
                Logger.Warn(e, "Failed to delete stale sitecustomize bytecode {Path}", cached.FullPath);
            }
        }
    }

    public string ProgressTitle => "Unpacking prerequisites...";
}
