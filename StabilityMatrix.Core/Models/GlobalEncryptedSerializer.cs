using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using DeviceId;

namespace StabilityMatrix.Core.Models;

internal record struct KeyInfo(byte[] Key, byte[] Salt, int Iterations);

/// <summary>
/// Encrypted MessagePack Serializer that uses a global key derived from the computer's SID.
/// Header contains additional random entropy as a salt that is used in decryption.
/// </summary>
public static class GlobalEncryptedSerializer
{
    private const int KeySize = 32;
    private const int Iterations = 300;
    private const int SaltSize = 16;

    public static T Deserialize<T>(ReadOnlySpan<byte> data)
    {
        // Get salt from start of file
        var salt = data[..SaltSize].ToArray();
        // Get encrypted json from rest of file
        var encryptedJson = data[SaltSize..];

        var json = DecryptBytes(encryptedJson, salt);
        
        return JsonSerializer.Deserialize<T>(json)
               ?? throw new Exception("Deserialize returned null");
    }

    public static byte[] Serialize<T>(T obj)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(obj);
        var (encrypted, salt) = EncryptBytes(json);
        // Prepend salt to encrypted json
        var fileBytes = salt.Concat(encrypted).ToArray();
        
        return fileBytes;
    }
    
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
                
                using var rfc2898 = new Rfc2898DeriveBytes(passwordByteArray, salt, iterations, HashAlgorithmName.SHA512);
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

    private static byte[] DecryptBytes(ReadOnlySpan<byte> encryptedData, byte[] salt)
    {
        var key = DeriveKey(GetComputerKeyPhrase(), salt, Iterations, KeySize);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = salt;
        aes.Padding = PaddingMode.PKCS7;
        aes.Mode = CipherMode.CBC;

        var transform = aes.CreateDecryptor();
        return transform.TransformFinalBlock(encryptedData.ToArray(), 0, encryptedData.Length);
    }
}
