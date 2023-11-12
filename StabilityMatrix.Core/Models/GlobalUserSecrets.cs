using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeviceId;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Core.Models;

internal record struct KeyInfo(byte[] Key, byte[] Salt, int Iterations);

/// <summary>
/// Global instance of user secrets.
/// Stored in %APPDATA%\StabilityMatrix\user-secrets.data
/// </summary>
public class GlobalUserSecrets
{
    private const int KeySize = 32;
    private const int Iterations = 300;
    private const int SaltSize = 16;

    [JsonIgnore]
    public static FilePath File { get; } = GlobalConfig.HomeDir + "user-secrets.data";

    public Dictionary<string, string> PatreonCookies { get; set; } = new();

    public string? CivitApiToken { get; set; }

    private static string? GetComputerSid()
    {
        var deviceId = new DeviceIdBuilder()
            .AddMachineName()
            .AddOsVersion()
            .OnWindows(
                windows =>
                    windows
                        .AddProcessorId()
                        .AddMotherboardSerialNumber()
                        .AddSystemDriveSerialNumber()
            )
            .OnLinux(linux => linux.AddMotherboardSerialNumber().AddSystemDriveSerialNumber())
            .OnMac(mac => mac.AddSystemDriveSerialNumber().AddPlatformSerialNumber())
            .ToString();

        return deviceId;
    }

    private static SecureString GetComputerKeyPhrase()
    {
        var keySource = GetComputerSid();
        // If no sid, use username as fallback
        keySource ??= Environment.UserName;

        // XOR with fixed constant
        const string keyPhrase = "StabilityMatrix";
        var result = new SecureString();

        for (var i = 0; i < keySource.Length; i++)
        {
            result.AppendChar((char)(keySource[i] ^ keyPhrase[i % keyPhrase.Length]));
        }

        return result;
    }

    private static KeyInfo DeriveKeyWithSalt(
        SecureString password,
        int saltLength,
        int iterations,
        int keyLength
    )
    {
        var salt = RandomNumberGenerator.GetBytes(saltLength);
        var key = DeriveKey(password, salt, iterations, keyLength);
        return new KeyInfo(key, salt, iterations);
    }

    private static byte[] DeriveKey(
        SecureString password,
        byte[] salt,
        int iterations,
        int keyLength
    )
    {
        var ptr = Marshal.SecureStringToBSTR(password);
        try
        {
            var length = Marshal.ReadInt32(ptr, -4);
            var passwordByteArray = new byte[length];
            var handle = GCHandle.Alloc(passwordByteArray, GCHandleType.Pinned);
            try
            {
                for (var i = 0; i < length; i++)
                {
                    passwordByteArray[i] = Marshal.ReadByte(ptr, i);
                }

                using var rfc2898 = new Rfc2898DeriveBytes(passwordByteArray, salt, iterations);
                return rfc2898.GetBytes(keyLength);
            }
            finally
            {
                Array.Clear(passwordByteArray, 0, passwordByteArray.Length);
                handle.Free();
            }
        }
        finally
        {
            Marshal.ZeroFreeBSTR(ptr);
        }
    }

    private static (byte[], byte[]) EncryptBytes(byte[] data)
    {
        var keyInfo = DeriveKeyWithSalt(GetComputerKeyPhrase(), SaltSize, Iterations, KeySize);

        using var aes = Aes.Create();
        aes.Key = keyInfo.Key;
        aes.IV = keyInfo.Salt;
        aes.Padding = PaddingMode.PKCS7;
        aes.Mode = CipherMode.CBC;

        var transform = aes.CreateEncryptor();
        return (transform.TransformFinalBlock(data, 0, data.Length), keyInfo.Salt);
    }

    private static byte[] DecryptBytes(IReadOnlyCollection<byte> encryptedData, byte[] salt)
    {
        var key = DeriveKey(GetComputerKeyPhrase(), salt, Iterations, KeySize);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = salt;
        aes.Padding = PaddingMode.PKCS7;
        aes.Mode = CipherMode.CBC;

        var transform = aes.CreateDecryptor();
        return transform.TransformFinalBlock(encryptedData.ToArray(), 0, encryptedData.Count);
    }

    public void SaveToFile()
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(this);
        var (encrypted, salt) = EncryptBytes(json);
        // Prepend salt to encrypted json
        var fileBytes = salt.Concat(encrypted).ToArray();

        File.WriteAllBytes(fileBytes);
    }

    public static GlobalUserSecrets? LoadFromFile()
    {
        File.Info.Refresh();

        if (!File.Exists)
        {
            return new GlobalUserSecrets();
        }

        var fileBytes = File.ReadAllBytes();

        // Get salt from start of file
        var salt = fileBytes.AsSpan(0, SaltSize).ToArray();
        // Get encrypted json from rest of file
        var encryptedJson = fileBytes.AsSpan(SaltSize).ToArray();

        var json = DecryptBytes(encryptedJson, salt);
        return JsonSerializer.Deserialize<GlobalUserSecrets>(json);
    }
}
