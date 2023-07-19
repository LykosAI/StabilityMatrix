using System.CodeDom;
using System.CodeDom.Compiler;

namespace StabilityMatrix.Core.Extensions;

public static class StringExtensions
{
    /// <summary>
    /// Converts string to repr
    /// </summary>
    public static string ToRepr(this string str)
    {
        using var writer = new StringWriter();
        using var provider = CodeDomProvider.CreateProvider("CSharp");
        
        provider.GenerateCodeFromExpression(
            new CodePrimitiveExpression(str), 
            writer, 
            new CodeGeneratorOptions {IndentString = "\t"});
        
        var literal = writer.ToString();
        // Replace split lines
        literal = literal.Replace($"\" +{Environment.NewLine}\t\"", "");
        // Surround with single quotes
        literal = $"'{literal}'";
        return literal;
    }
    
    /// <summary>
    /// Counts continuous sequence of a character
    /// from the start of the string
    /// </summary>
    public static int CountStart(this string str, char c)
    {
        var count = 0;
        foreach (var ch in str)
        {
            if (ch == c)
            {
                count++;
            }
            else
            {
                break;
            }
        }
        return count;
    }
}
