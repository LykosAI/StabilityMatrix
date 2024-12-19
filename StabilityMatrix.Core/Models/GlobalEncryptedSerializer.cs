using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using DeviceId;

namespace StabilityMatrix.Core.Models;

/// <summary>
/// Encrypted MessagePack Serializer that uses a global key derived from the computer's SID.
/// Header contains additional random entropy as a salt that is used in decryption.
/// </summary>
public static class GlobalEncryptedSerializer
{
    internal static KeyInfo KeyInfoV1 => new(FormatVersion.V1, 32, 16, 300);

    internal static KeyInfo KeyInfoV2 => new(FormatVersion.V2, 32, 16, 300);

    private static byte[] HeaderPrefixV2 => [0x4C, 0x4B, 0x1F, 0x45, 0x5C, 0x02, 0x00];

    internal readonly record struct KeyInfo(FormatVersion Version, int KeySize, int SaltSize, int Iterations);

    internal enum FormatVersion : byte
    {
        /// <summary>
        /// Version 1
        /// Original format, no header.
        /// File: [(16 bytes salt), (Encrypted json data)]
        /// </summary>
        V1 = 1,

        /// <summary>
        /// Version 2+
        /// Header: [4C, 4B, 1F, 45, 5C, ??, 00] where ?? is the version byte.
        /// File: [(Header), (SaltSize bytes salt), (Encrypted json data)]
        /// </summary>
        V2 = 2
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct HeaderV2
    {
        private unsafe fixed byte Prefix[7];

        public int KeySize;

        public int SaltSize;

        public int Iterations;

        public HeaderV2()
        {
            unsafe
            {
                Prefix[0] = 0x4C;
                Prefix[1] = 0x4B;
                Prefix[2] = 0x1F;
                Prefix[3] = 0x45;
                Prefix[4] = 0x5C;
                Prefix[5] = 0x02;
                Prefix[6] = 0x00;
            }
        }
    }

    public static T Deserialize<T>(ReadOnlySpan<byte> data)
    {
        // Header prefix, use v2
        if (data.StartsWith(HeaderPrefixV2))
        {
            var json = DeserializeToBytesV2(data);
            return JsonSerializer.Deserialize<T>(json)
                ?? throw new InvalidOperationException("Deserialize returned null");
        }
        // No header, use v1
        else
        {
            var json = DeserializeToBytesV1(data);
            return JsonSerializer.Deserialize<T>(json)
                ?? throw new InvalidOperationException("Deserialize returned null");
        }
    }

    public static byte[] Serialize<T>(T obj)
    {
        return Serialize(obj, KeyInfoV2);
    }

    internal static byte[] Serialize<T>(T obj, KeyInfo keyInfo)
    {
        switch (keyInfo.Version)
        {
            case <= FormatVersion.V1:
            {
                var json = JsonSerializer.SerializeToUtf8Bytes(obj);
                return SerializeToBytesV1(json);
            }
            case >= FormatVersion.V2:
            {
                var json = JsonSerializer.SerializeToUtf8Bytes(obj);
                return SerializeToBytesV2(json, keyInfo);
            }
        }
    }

    private static byte[] SerializeToBytesV1(byte[] data)
    {
        // Get encrypted bytes and salt
        var password = GetComputerKeyPhrase(KeyInfoV1.Version);
        var (encrypted, salt) = EncryptBytes(data, password, KeyInfoV1);

        // Prepend salt to encrypted json
        var fileData = salt.Concat(encrypted);

        return fileData.ToArray();
    }

    private static byte[] SerializeToBytesV2(byte[] data, KeyInfo keyInfo)
    {
        // Create header
        var headerSize = Marshal.SizeOf<HeaderV2>();
        var header = new HeaderV2
        {
            KeySize = keyInfo.KeySize,
            SaltSize = keyInfo.SaltSize,
            Iterations = keyInfo.Iterations
        };
        var headerBytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref header, 1));
        Debug.Assert(headerBytes.Length == headerSize);

        // Get salt + encrypted json
        var password = GetComputerKeyPhrase(keyInfo.Version);
        var (encrypted, salt) = EncryptBytes(data, password, keyInfo);
        Debug.Assert(salt.Length == keyInfo.SaltSize);

        // Write result as [header, salt, encrypted]
        var result = new byte[headerBytes.Length + salt.Length + encrypted.Length];

        headerBytes.CopyTo(result.AsSpan(0, headerSize));
        salt.CopyTo(result.AsSpan(headerSize, keyInfo.SaltSize));
        encrypted.CopyTo(result.AsSpan(headerSize + keyInfo.SaltSize));

        return result;
    }

    private static byte[] DeserializeToBytesV1(ReadOnlySpan<byte> data)
    {
        var keyInfo = KeyInfoV1;

        // Get salt from start of file
        var salt = data[..keyInfo.SaltSize].ToArray();

        // Get encrypted json from rest of file
        var encryptedJson = data[keyInfo.SaltSize..];

        var password = GetComputerKeyPhrase(keyInfo.Version);
        return DecryptBytes(encryptedJson, salt, password, keyInfo);
    }

    private static byte[] DeserializeToBytesV2(ReadOnlySpan<byte> data)
    {
        // Read header
        var headerSize = Marshal.SizeOf<HeaderV2>();
        var header = MemoryMarshal.Read<HeaderV2>(data[..Marshal.SizeOf<HeaderV2>()]);

        // Read Salt
        var salt = data[headerSize..(headerSize + header.SaltSize)].ToArray();

        // Rest of data is encrypted json
        var encryptedData = data[(headerSize + header.SaltSize)..];

        var keyInfo = new KeyInfo(FormatVersion.V2, header.KeySize, header.SaltSize, header.Iterations);

        var password = GetComputerKeyPhrase(keyInfo.Version);
        return DecryptBytes(encryptedData, salt, password, keyInfo);
    }

    private static string? GetComputerSid(FormatVersion version)
    {
        return version switch
        {
            FormatVersion.V1
                => new DeviceIdBuilder()
                    .AddMachineName()
                    .AddOsVersion()
                    .OnWindows(
                        windows =>
                            windows.AddProcessorId().AddMotherboardSerialNumber().AddSystemDriveSerialNumber()
                    )
                    .OnLinux(linux => linux.AddMotherboardSerialNumber().AddSystemDriveSerialNumber())
                    .OnMac(mac => mac.AddSystemDriveSerialNumber().AddPlatformSerialNumber())
                    .ToString(),
            // v2: Removed OsVersion since it's updated often on macOS
            FormatVersion.V2
                => new DeviceIdBuilder()
                    .AddMachineName()
                    .OnWindows(
                        windows =>
                            windows.AddProcessorId().AddMotherboardSerialNumber().AddSystemDriveSerialNumber()
                    )
                    .OnLinux(linux => linux.AddMotherboardSerialNumber().AddSystemDriveSerialNumber())
                    .OnMac(mac => mac.AddSystemDriveSerialNumber().AddPlatformSerialNumber())
                    .ToString(),
            _ => throw new ArgumentOutOfRangeException(nameof(version))
        };
    }

    private static SecureString GetComputerKeyPhrase(FormatVersion version)
    {
        var keySource = GetComputerSid(version);
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

    private static byte[] GenerateSalt(int length)
    {
        return RandomNumberGenerator.GetBytes(length);
    }

    private static byte[] DeriveKey(SecureString password, byte[] salt, int iterations, int keyLength)
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

                using var rfc2898 = new Rfc2898DeriveBytes(
                    passwordByteArray,
                    salt,
                    iterations,
                    HashAlgorithmName.SHA512
                );
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

    internal static (byte[] EncryptedData, byte[] Salt) EncryptBytes(
        byte[] data,
        SecureString password,
        KeyInfo keyInfo
    )
    {
        var salt = GenerateSalt(keyInfo.SaltSize);
        var key = DeriveKey(password, salt, keyInfo.Iterations, keyInfo.KeySize);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = salt;
        aes.Padding = PaddingMode.PKCS7;
        aes.Mode = CipherMode.CBC;

        var transform = aes.CreateEncryptor();
        return (transform.TransformFinalBlock(data, 0, data.Length), salt);
    }

    internal static byte[] DecryptBytes(
        ReadOnlySpan<byte> encryptedData,
        byte[] salt,
        SecureString password,
        KeyInfo keyInfo
    )
    {
        var key = DeriveKey(password, salt, keyInfo.Iterations, keyInfo.KeySize);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = salt;
        aes.Padding = PaddingMode.PKCS7;
        aes.Mode = CipherMode.CBC;

        var transform = aes.CreateDecryptor();
        return transform.TransformFinalBlock(encryptedData.ToArray(), 0, encryptedData.Length);
    }
}
