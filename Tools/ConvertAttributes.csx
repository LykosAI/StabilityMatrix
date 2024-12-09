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

var attributePattern = new Regex(@"\[(?<AttributeName>\w+)(?:\((typeof\((?<TypeName>\w+)\))?\))?\]");

var totalFiles = 0;

foreach (var file in csFiles)
{
    var content = File.ReadAllText(file);
    var updatedContent = content;
    
    var attributeMatches = attributePattern.Matches(content);
    
    // Get Singleton and Transient attributes
    var singletonMatch = attributeMatches.FirstOrDefault(m => m.Groups["AttributeName"].Value == "Singleton");
    var transientMatch = attributeMatches.FirstOrDefault(m => m.Groups["AttributeName"].Value == "Transient");

    // Get type name from file name
    var targetTypeName = Path.GetFileNameWithoutExtension(file);
    if (targetTypeName.Contains('.'))
    {
        targetTypeName = targetTypeName.Split('.').First();
    }

    // Replace SingletonAttribute with RegisterSingletonAttribute
    if (singletonMatch != null)
    {
        var interfaceTypeName = singletonMatch.Groups["TypeName"].Value;
        var isMulti = interfaceTypeName == "BasePackage";
        
        var replacement = string.IsNullOrWhiteSpace(interfaceTypeName)
            ? $"[RegisterSingleton<{targetTypeName}>"
            : $"[RegisterSingleton<{interfaceTypeName}, {targetTypeName}>";
        
        if (isMulti)
        {
            replacement += "(Duplicate = DuplicateStrategy.Append)";
        }
        
        replacement += "]";
        
        // Replace
        Console.WriteLine($"Replacing {singletonMatch.Value} with {replacement}");
        updatedContent = updatedContent.Remove(singletonMatch.Index, singletonMatch.Length);
        updatedContent = updatedContent.Insert(singletonMatch.Index, replacement);
    }
    else if (transientMatch != null)
    {
        var interfaceTypeName = transientMatch.Groups["TypeName"].Value;
        var isMulti = interfaceTypeName == "BasePackage";
                
        var replacement = string.IsNullOrWhiteSpace(interfaceTypeName)
            ? $"[RegisterTransient<{targetTypeName}>"
            : $"[RegisterTransient<{interfaceTypeName}, {targetTypeName}>";
        
        if (isMulti)
        {
            replacement += "(Duplicate = DuplicateStrategy.Append)";
        }
        
        replacement += "]";
        
        // Replace
        Console.WriteLine($"Replacing {transientMatch.Value} with {replacement}");
        updatedContent = updatedContent.Remove(transientMatch.Index, transientMatch.Length);
        updatedContent = updatedContent.Insert(transientMatch.Index, replacement);
    }

    if (content == updatedContent)
    {
        // No changes
        continue;
    }
    
    // If file not containing `[ManagedService]` / `[..., ManagedService] replace using
    if (!updatedContent.Contains("ManagedService]"))
    {
        updatedContent = Regex.Replace(updatedContent, @"using StabilityMatrix\.Core\.Attributes;", "using Injectio.Attributes;");
    }
    else
    {
        // Otherwise just add after
        var originalUsing = Regex.Match(updatedContent, @"using StabilityMatrix\.Core\.Attributes;");
        updatedContent = updatedContent.Insert(originalUsing.Index + originalUsing.Length, "\nusing Injectio.Attributes;");
    }
    
    if (checkOnly)
    {
        Console.WriteLine($"Would modify: {file}");
        // Console.WriteLine("<--");
        // Console.WriteLine(updatedContent);
        // Console.WriteLine("-->");
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
