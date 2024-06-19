using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Python;

public record QueryTclTkLibraryResult(string? TclLibrary, string? TkLibrary);

[JsonSerializable(typeof(QueryTclTkLibraryResult))]
internal partial class QueryTclTkLibraryResultJsonContext : JsonSerializerContext;
