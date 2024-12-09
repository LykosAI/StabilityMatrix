// Run with: `dotnet script ./Tools/ConvertAttributes.csx`
#r "nuget: System.Text.RegularExpressions, 4.3.1"
#r "nuget: Ignore, 0.2.1"

using System.IO;
using System.Text.RegularExpressions;
using Ignore;

// Options:
// Only checks files if --write is not provided
var checkOnly = !Args.Contains("--write");
// 
    
var projectDirectory = Directory.GetCurrentDirectory();
var gitIgnoreFile = Path.Combine(projectDirectory, ".gitignore");

var ignore = new Ignore.Ignore();
if (File.Exists(gitIgnoreFile))
{
    var gitIgnoreContent = File.ReadAllText(gitIgnoreFile);
    ignore.Add(gitIgnoreContent);
}

var csFiles = new List<string>();
foreach (var file in Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories))
{
    if (ignore.IsIgnored(file) || file.EndsWith(".Designer.cs") || file.EndsWith(".g.cs") || file.EndsWith(".generated.cs"))
    {
        continue;
    }
    var parts = file.Split(Path.DirectorySeparatorChar);
    if (parts.Contains("bin") || parts.Contains("obj"))
    {
        continue;
    }
    csFiles.Add(file);
}

var totalFiles = 0;

foreach (var file in csFiles)
{
    var content = File.ReadAllText(file);
    var updatedContent = content;

    // Check for complex SingletonAttribute arguments
    var singletonComplexMatch = Regex.Match(content, @"\[Singleton\((.+?)\)\]");
    if (singletonComplexMatch.Success)
    {
        Console.WriteLine($"Warning: Ignored complex SingletonAttribute in file: {file}");
        continue;
    }

    // Check for complex TransientAttribute arguments
    var transientComplexMatch = Regex.Match(content, @"\[Transient\((.+?)\)\]");
    if (transientComplexMatch.Success)
    {
        Console.WriteLine($"Warning: Ignored complex TransientAttribute in file: {file}");
        continue;
    }
    
    // Get type name from file name
    var targetTypeName = Path.GetFileNameWithoutExtension(file);

    // Replace SingletonAttribute with RegisterSingletonAttribute
    updatedContent = Regex.Replace(updatedContent, @"\[Singleton\]", $"[RegisterSingleton<{targetTypeName}>]");

    // Replace TransientAttribute with RegisterTransientAttribute
    updatedContent = Regex.Replace(updatedContent, @"\[Transient\]", $"[RegisterTransient<{targetTypeName}>]");

    if (content == updatedContent)
    {
        // No changes
        continue;
    }
    
    // Replace using directive
    updatedContent = Regex.Replace(updatedContent, @"using StabilityMatrix\.Core\.Attributes;", "using Injectio.Attributes;");
    
    if (checkOnly)
    {
        Console.WriteLine($"Would modify: {file}");
        Console.WriteLine("<--");
        Console.WriteLine(updatedContent);
        Console.WriteLine("-->");
    }
    else
    {
        File.WriteAllText(file, updatedContent);
        Console.WriteLine($"Updated file: {file}");
    }
    totalFiles++;
}

if (checkOnly)
{
    Console.WriteLine($"Check complete. {totalFiles} files would be modified.");
}
else
{
    Console.WriteLine($"Update complete. {totalFiles} files modified.");
}
