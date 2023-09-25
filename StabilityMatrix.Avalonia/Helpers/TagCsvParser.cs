using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using Sylvan.Data.Csv;
using Sylvan;
using Sylvan.Data;

namespace StabilityMatrix.Avalonia.Helpers;

public class TagCsvParser
{
    private readonly Stream stream;
    
    public TagCsvParser(Stream stream)
    {
        this.stream = stream;
    }
    
    public async IAsyncEnumerable<TagCsvEntry> ParseAsync()
    {
        var pool = new StringPool();
        var options = new CsvDataReaderOptions
        {
            StringFactory = pool.GetString,
            HasHeaders = false,
        };
        
        using var textReader = new StreamReader(stream);
        await using var dataReader = await CsvDataReader.CreateAsync(textReader, options);
        
        while (await dataReader.ReadAsync())
        {
            var entry = new TagCsvEntry
            {
                Name = dataReader.GetString(0),
                Type = dataReader.GetInt32(1),
                Count = dataReader.GetInt32(2),
                Aliases = dataReader.GetString(3),
            };
            yield return entry;
        }
        
        /*var dataBinderOptions = new DataBinderOptions
        {
            BindingMode = DataBindingMode.Any
        };*/
        /*var results = dataReader.GetRecordsAsync<TagCsvEntry>(dataBinderOptions);
        return results;*/
    }

    public async Task<Dictionary<string, TagCsvEntry>> GetDictionaryAsync()
    {
        var dict = new Dictionary<string, TagCsvEntry>();
        
        await foreach (var entry in ParseAsync())
        {
            if (entry.Name is null || entry.Type is null)
            {
                continue;
            }
            
            dict.Add(entry.Name, entry);
        }
        
        return dict;
    }
}
