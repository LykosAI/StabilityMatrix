using OneOf;

namespace StabilityMatrix.Core.Processes;

[GenerateOneOf]
public partial class Argument : OneOfBase<string, (string, string)> { }
