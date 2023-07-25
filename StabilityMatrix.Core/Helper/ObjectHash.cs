using System.Text;
using System.Text.Json;

namespace StabilityMatrix.Core.Helper;

public class ObjectHash
{
    /// <summary>
    /// Return a GUID based on the MD5 hash of the JSON representation of the object.
    /// </summary>
    public static Guid GetMd5Guid<T>(T obj)
    {
        var json = JsonSerializer.Serialize(obj);
        var bytes = Encoding.UTF8.GetBytes(json);
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(bytes);
        return new Guid(hash);
    }
}
