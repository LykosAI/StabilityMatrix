using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class TarGZipExtractorTests
{
    private string workspace = null!;
    private string output = null!;
    private string archive = null!;

    [TestInitialize]
    public void Initialize()
    {
        workspace = Path.Combine(Path.GetTempPath(), $"tar-extraction-{Guid.NewGuid():N}");
        output = Path.Combine(workspace, "output with spaces");
        archive = Path.Combine(workspace, "archive.tar.gz");
        Directory.CreateDirectory(workspace);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(workspace, recursive: true);

    [TestMethod]
    public async Task ExtractAsync_RegularFilesAndDirectories()
    {
        WriteArchive(
            new PaxTarEntry(TarEntryType.Directory, "node/lib/"),
            FileEntry("node/lib/file with spaces", "contents")
        );

        await TarGZipExtractor.ExtractAsync(archive, output);

        Assert.AreEqual("contents", File.ReadAllText(Path.Combine(output, "node/lib/file with spaces")));
    }

    [DataTestMethod]
    [DataRow("node-v20.19.3-linux-x64")]
    [DataRow("node-v20.19.3-darwin-arm64")]
    public async Task ExtractAsync_NodeLinksBeforeTargetsAndExecutableMode(string folder)
    {
        RequireSymlinks();
        var names = new[] { "npm", "npx", "corepack" };
        var targets = new[]
        {
            "../lib/node_modules/npm/bin/npm-cli.js",
            "../lib/node_modules/npm/bin/npx-cli.js",
            "../lib/node_modules/corepack/dist/corepack.js",
        };
        var entries = names.Select((name, i) => LinkEntry($"{folder}/bin/{name}", targets[i])).ToList();
        entries.Add(FileEntry($"{folder}/bin/node", "node"));
        entries.AddRange(targets.Select(target => FileEntry($"{folder}/bin/{target}", "script")));
        WriteArchive(entries.ToArray());

        await TarGZipExtractor.ExtractAsync(archive, output);

        // The installer moves the whole distribution after extraction; relative links must survive it.
        var installed = Path.Combine(output, "nodejs");
        Directory.Move(Path.Combine(output, folder), installed);
        for (var i = 0; i < names.Length; i++)
        {
            var path = Path.Combine(installed, "bin", names[i]);
            Assert.AreEqual(targets[i], new FileInfo(path).LinkTarget);
            Assert.AreEqual("script", File.ReadAllText(path));
        }
        if (!OperatingSystem.IsWindows())
        {
            Assert.IsTrue(
                File.GetUnixFileMode(Path.Combine(installed, "bin/node")).HasFlag(UnixFileMode.UserExecute)
            );
        }
    }

    [DataTestMethod]
    [DataRow("../outside")]
    [DataRow("../output with spaces-sibling/outside")]
    [DataRow("/absolute")]
    [DataRow("C:/outside")]
    [DataRow("C:outside")]
    [DataRow("\\\\server\\share\\outside")]
    [DataRow("..\\outside")]
    public async Task ExtractAsync_RejectsUnsafeEntryPaths(string name)
    {
        WriteArchive(FileEntry(name, "unsafe"));

        await Assert.ThrowsExceptionAsync<IOException>(() => TarGZipExtractor.ExtractAsync(archive, output));

        Assert.IsFalse(File.Exists(Path.Combine(workspace, "outside")));
    }

    [DataTestMethod]
    [DataRow("../../../outside")]
    [DataRow("/absolute")]
    [DataRow("C:/outside")]
    [DataRow("..\\..\\outside")]
    public async Task ExtractAsync_RejectsUnsafeLinkTargets(string target)
    {
        WriteArchive(LinkEntry("node/bin/npm", target));

        await Assert.ThrowsExceptionAsync<IOException>(() => TarGZipExtractor.ExtractAsync(archive, output));
    }

    [DataTestMethod]
    [DataRow(TarEntryType.HardLink)]
    [DataRow(TarEntryType.Fifo)]
    [DataRow(TarEntryType.CharacterDevice)]
    [DataRow(TarEntryType.BlockDevice)]
    public async Task ExtractAsync_RejectsHardLinksAndSpecialFiles(TarEntryType type)
    {
        var entry = new PaxTarEntry(type, "unsafe");
        if (type == TarEntryType.HardLink)
        {
            entry.LinkName = "../outside";
        }
        WriteArchive(entry);

        await Assert.ThrowsExceptionAsync<IOException>(() => TarGZipExtractor.ExtractAsync(archive, output));
    }

    [TestMethod]
    public async Task ExtractAsync_RejectsMissingLinkTarget()
    {
        WriteArchive(LinkEntry("node/bin/npm", "../missing"));

        await Assert.ThrowsExceptionAsync<IOException>(() => TarGZipExtractor.ExtractAsync(archive, output));
    }

    [TestMethod]
    public async Task ExtractAsync_RejectsTargetNotExtractedFromArchive()
    {
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "existing"), "sentinel");
        WriteArchive(LinkEntry("link", "existing"));

        await Assert.ThrowsExceptionAsync<IOException>(() => TarGZipExtractor.ExtractAsync(archive, output));

        Assert.AreEqual("sentinel", File.ReadAllText(Path.Combine(output, "existing")));
    }

    [TestMethod]
    public async Task ExtractAsync_RejectsAbsoluteEntryInsideDestination()
    {
        WriteArchive(FileEntry(Path.Combine(output, "file").Replace('\\', '/'), "unsafe"));

        await Assert.ThrowsExceptionAsync<IOException>(() => TarGZipExtractor.ExtractAsync(archive, output));
    }

    [TestMethod]
    public async Task ExtractAsync_RejectsLinkedDestinationRoot()
    {
        RequireSymlinks();
        var outside = Path.Combine(workspace, "outside");
        Directory.CreateDirectory(outside);
        Directory.CreateSymbolicLink(output, outside);
        WriteArchive(FileEntry("file", "unsafe"));

        await Assert.ThrowsExceptionAsync<IOException>(() => TarGZipExtractor.ExtractAsync(archive, output));

        Assert.IsFalse(File.Exists(Path.Combine(outside, "file")));
    }

    [TestMethod]
    public async Task ExtractAsync_RejectsArchiveDirectoryLinks()
    {
        WriteArchive(
            new PaxTarEntry(TarEntryType.Directory, "node/lib/"),
            LinkEntry("node/bin", "lib"),
            FileEntry("node/bin/file", "unsafe")
        );

        await Assert.ThrowsExceptionAsync<IOException>(() => TarGZipExtractor.ExtractAsync(archive, output));

        Assert.IsFalse(File.Exists(Path.Combine(output, "node/lib/file")));
    }

    [TestMethod]
    public async Task ExtractAsync_RejectsLinkChains()
    {
        RequireSymlinks();
        WriteArchive(FileEntry("file", "safe"), LinkEntry("link", "file"), LinkEntry("chain", "link"));

        await Assert.ThrowsExceptionAsync<IOException>(() => TarGZipExtractor.ExtractAsync(archive, output));
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExtractAsync_RejectsExistingDirectoryLink(bool broken)
    {
        RequireSymlinks();
        var outside = Path.Combine(workspace, "outside");
        if (!broken)
        {
            Directory.CreateDirectory(outside);
            File.WriteAllText(Path.Combine(outside, "file"), "sentinel");
        }
        Directory.CreateDirectory(output);
        Directory.CreateSymbolicLink(Path.Combine(output, "node"), outside);
        WriteArchive(FileEntry("node/file", "unsafe"));

        await Assert.ThrowsExceptionAsync<IOException>(() => TarGZipExtractor.ExtractAsync(archive, output));

        if (!broken)
        {
            Assert.AreEqual("sentinel", File.ReadAllText(Path.Combine(outside, "file")));
        }
        else
        {
            Assert.IsFalse(Directory.Exists(outside));
        }
    }

    [TestMethod]
    public async Task ExtractAsync_RejectsExistingFileLink()
    {
        RequireSymlinks();
        var outside = Path.Combine(workspace, "outside");
        File.WriteAllText(outside, "sentinel");
        Directory.CreateDirectory(output);
        File.CreateSymbolicLink(Path.Combine(output, "file"), outside);
        WriteArchive(FileEntry("file", "unsafe"));

        await Assert.ThrowsExceptionAsync<IOException>(() => TarGZipExtractor.ExtractAsync(archive, output));

        Assert.AreEqual("sentinel", File.ReadAllText(outside));
    }

    [TestMethod]
    public async Task ExtractAsync_NormalizesLinkTargetBeforeCreatingLink()
    {
        RequireSymlinks();
        var outside = Path.Combine(workspace, "outside");
        Directory.CreateDirectory(Path.Combine(outside, "directory"));
        File.WriteAllText(Path.Combine(outside, "file"), "sentinel");
        Directory.CreateDirectory(output);
        Directory.CreateSymbolicLink(Path.Combine(output, "existing"), Path.Combine(outside, "directory"));
        WriteArchive(FileEntry("file", "safe"), LinkEntry("link", "existing/../file"));

        await TarGZipExtractor.ExtractAsync(archive, output);

        Assert.AreEqual("file", new FileInfo(Path.Combine(output, "link")).LinkTarget);
        Assert.AreEqual("safe", File.ReadAllText(Path.Combine(output, "link")));
        Assert.AreEqual("sentinel", File.ReadAllText(Path.Combine(outside, "file")));
    }

    private void RequireSymlinks()
    {
        var target = Path.Combine(workspace, "symlink-probe-target");
        var link = Path.Combine(workspace, "symlink-probe");
        File.WriteAllText(target, "probe");
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Assert.Inconclusive($"This host cannot create symlinks: {e.Message}");
        }
    }

    private void WriteArchive(params TarEntry[] entries)
    {
        using var stream = File.Create(archive);
        using var gzip = new GZipStream(stream, CompressionMode.Compress);
        using var writer = new TarWriter(gzip, TarEntryFormat.Pax);
        foreach (var entry in entries)
        {
            writer.WriteEntry(entry);
            entry.DataStream?.Dispose();
        }
    }

    private static PaxTarEntry FileEntry(string name, string contents) =>
        new(TarEntryType.RegularFile, name)
        {
            DataStream = new MemoryStream(Encoding.UTF8.GetBytes(contents)),
            Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
        };

    private static PaxTarEntry LinkEntry(string name, string target) =>
        new(TarEntryType.SymbolicLink, name) { LinkName = target };
}
