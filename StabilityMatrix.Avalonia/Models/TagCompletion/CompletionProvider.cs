using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoComplete.Builders;
using AutoComplete.Clients.IndexSearchers;
using AutoComplete.DataStructure;
using AutoComplete.Domain;
using NLog;
using StabilityMatrix.Avalonia.Controls.CodeCompletion;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Avalonia.Models.TagCompletion;

public class CompletionProvider : ICompletionProvider
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly Dictionary<string, TagCsvEntry> entries = new();
    
    private InMemoryIndexSearcher? searcher;
    
    public bool IsLoaded => searcher is not null;
    
    public async Task LoadFromFile(FilePath path, bool recreate = false)
    {
        // Get Blake3 hash of file
        var hash = await FileHash.GetBlake3Async(path);
        
        Logger.Trace("Loading tags from {Path} with Blake3 hash {Hash}", path, hash);
        
        // Check for AppData/StabilityMatrix/Temp/Tags/<hash>/*.bin
        var tempTagsDir = GlobalConfig.HomeDir.JoinDir("Temp", "Tags");
        tempTagsDir.Create();
        var hashDir = tempTagsDir.JoinDir(hash);
        
        var headerFile = hashDir.JoinFile("header.bin");
        var indexFile = hashDir.JoinFile("index.bin");
        var tailFile = hashDir.JoinFile("tail.bin");

        entries.Clear();
        
        // If directory or any file is missing, rebuild the index
        if (recreate || !(hashDir.Exists && headerFile.Exists && indexFile.Exists && tailFile.Exists))
        {
            Logger.Trace("Creating new index for {Path}", hashDir);
            hashDir.Create();
            
            await using var headerStream = headerFile.Info.OpenWrite();
            await using var indexStream = indexFile.Info.OpenWrite();
            await using var tailStream = tailFile.Info.OpenWrite();
            
            var builder = new IndexBuilder(headerStream, indexStream, tailStream);
            
            // Parse csv
            var csvStream = path.Info.OpenRead();
            var parser = new TagCsvParser(csvStream);

            await foreach (var entry in parser.ParseAsync())
            {
                if (string.IsNullOrWhiteSpace(entry.Name))
                {
                    continue;
                }
                // Add to index
                builder.Add(entry.Name);
                // Add to local dictionary
                entries.Add(entry.Name, entry);
            }
            
            builder.Build();
        }
        
        searcher = new InMemoryIndexSearcher(headerFile, indexFile, tailFile);
        searcher.Init();
    }
    
    /// <inheritdoc />
    public IEnumerable<ICompletionData> GetCompletions(string searchTerm, int itemsCount, bool suggest)
    {
        if (searcher is null)
        {
            throw new InvalidOperationException("Index is not loaded");
        }
        
        var searchOptions = new SearchOptions
        {
            Term = searchTerm,
            MaxItemCount = itemsCount,
            SuggestWhenFoundStartsWith = suggest
        };

        var result = searcher.Search(searchOptions);

        // No results
        if (result.ResultType == TrieNodeSearchResultType.NotFound)
        {
            Logger.Trace("No results for {Term}", searchTerm);
            return Array.Empty<ICompletionData>();
        }
        
        Logger.Trace("Got {Count} results for {Term}", result.Items.Length, searchTerm);
        
        // Get entry for each result
        var completions = new List<ICompletionData>();
        foreach (var item in result.Items)
        {
            if (entries.TryGetValue(item, out var entry))
            {
                var entryType = TagTypeExtensions.FromE621(entry.Type.GetValueOrDefault(-1));
                completions.Add(new TagCompletionData(entry.Name!, entryType));
            }
        }
        
        return completions;
    }
}
