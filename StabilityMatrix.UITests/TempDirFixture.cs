using System.Runtime.CompilerServices;

namespace StabilityMatrix.UITests;

public class TempDirFixture : IDisposable
{
    public static string ModuleTempDir { get; set; }

    static TempDirFixture()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "StabilityMatrixTest");

        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, true);
        }
        Directory.CreateDirectory(tempDir);

        ModuleTempDir = tempDir;

        // ReSharper disable once LocalizableElement
        Console.WriteLine($"Using temp dir: {ModuleTempDir}");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(ModuleTempDir))
        {
            // ReSharper disable once LocalizableElement
            Console.WriteLine($"Deleting temp dir: {ModuleTempDir}");
            Directory.Delete(ModuleTempDir, true);
        }

        GC.SuppressFinalize(this);
    }
}

[CollectionDefinition("TempDir")]
public class TempDirCollection : ICollectionFixture<TempDirFixture> { }
