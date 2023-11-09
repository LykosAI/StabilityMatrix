using System.Runtime.CompilerServices;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace StabilityMatrix.UITests;

public static class ModuleInit
{
    [ModuleInitializer]
    public static void Init() => VerifyAvalonia.Initialize();

    [ModuleInitializer]
    public static void InitOther() => VerifierSettings.InitializePlugins();

    [ModuleInitializer]
    public static void ConfigureVerify()
    {
        VerifyPhash.RegisterComparer("png");

        DerivePathInfo(
            (sourceFile, projectDirectory, type, method) =>
                new PathInfo(
                    directory: Path.Combine(projectDirectory, "Snapshots"),
                    typeName: type.Name,
                    methodName: method.Name
                )
        );
    }
}
