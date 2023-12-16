using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace StabilityMatrix.Core.Extensions;

public static class NullableExtensions
{
    /// <summary>
    /// Unwraps a nullable object, throwing an exception if it is null.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if (<typeparamref name="T"/>) <paramref name="obj"/> is null.
    /// </exception>
    [DebuggerStepThrough]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Unwrap<T>([NotNull] this T? obj, [CallerArgumentExpression("obj")] string? paramName = null)
        where T : class
    {
        if (obj is null)
        {
            throw new ArgumentNullException(paramName, $"Unwrap of a null value ({typeof(T)}) {paramName}.");
        }
        return obj;
    }
}
