using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Grammars;
using TextMateSharp.Internal.Grammars.Reader;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;

namespace StabilityMatrix.Avalonia.Extensions;

public static class TextMateExtensions
{
    
    public static IGrammar LoadGrammarFromStream(
        this Registry registry,
        Stream stream,
        int? initialLanguage = default,
        Dictionary<string, int>? embeddedLanguages = default)
    {
        IRawGrammar rawGrammar;
        using (var sr = new StreamReader(stream))
        {
            rawGrammar = GrammarReader.ReadGrammarSync(sr);
        }
        
        var locatorField = typeof(Registry).GetField("locator", BindingFlags.NonPublic | BindingFlags.Instance);
        var locator = (IRegistryOptions) locatorField!.GetValue(registry)!;
        
        var injections = locator.GetInjections(rawGrammar.GetScopeName());
        
        var syncRegistryField = typeof(Registry).GetField("syncRegistry", BindingFlags.NonPublic | BindingFlags.Instance);
        var syncRegistry = (SyncRegistry) syncRegistryField!.GetValue(registry)!;
        
        syncRegistry.AddGrammar(rawGrammar, injections);
        return registry.GrammarForScopeName(
            rawGrammar.GetScopeName(), 
            initialLanguage ?? 0, 
            embeddedLanguages ?? new Dictionary<string, int>());
    }
}
