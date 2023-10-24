using Blake3;

namespace StabilityMatrix.Core.Extensions;

public static class HashExtensions
{
    public static Guid ToGuid(this Hash hash)
    {
        return new Guid(hash.AsSpan());
    }
}
