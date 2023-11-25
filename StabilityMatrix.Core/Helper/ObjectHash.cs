using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StabilityMatrix.Core.Helper;

public static class ObjectHash
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

    /// <summary>
    /// Return a short Sha256 signature of a string
    /// </summary>
    public static string GetStringSignature(string? str)
    {
        if (str is null)
        {
            return "null";
        }

        if (string.IsNullOrEmpty(str))
        {
            return "";
        }

        var bytes = Encoding.UTF8.GetBytes(str);
        var hash = Convert.ToBase64String(SHA256.HashData(bytes));

        return $"[..{str.Length}, {hash[..7]}]";
    }
}
