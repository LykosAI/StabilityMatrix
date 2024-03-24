using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

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
    [ContractAnnotation("obj:null => halt; obj:notnull => notnull")]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static T Unwrap<T>(
        [System.Diagnostics.CodeAnalysis.NotNull] this T? obj,
        [CallerArgumentExpression("obj")] string? paramName = null
    )
        where T : class
    {
        if (obj is null)
        {
            throw new ArgumentNullException(paramName, $"Unwrap of a null value ({typeof(T)}) {paramName}.");
        }
        return obj;
    }

    /// <summary>
    /// Unwraps a nullable struct object, throwing an exception if it is null.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if (<typeparamref name="T"/>) <paramref name="obj"/> is null.
    /// </exception>
    [DebuggerStepThrough]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [ContractAnnotation("obj:null => halt")]
    public static T Unwrap<T>(
        [System.Diagnostics.CodeAnalysis.NotNull] this T? obj,
        [CallerArgumentExpression("obj")] string? paramName = null
    )
        where T : struct
    {
        if (obj is null)
        {
            throw new ArgumentNullException(paramName, $"Unwrap of a null value ({typeof(T)}) {paramName}.");
        }
        return obj.Value;
    }
}
