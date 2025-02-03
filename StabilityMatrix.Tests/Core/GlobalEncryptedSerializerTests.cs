using System.Security;
using System.Security.Cryptography;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Lykos;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class GlobalEncryptedSerializerTests
{
    [TestMethod]
    public void Serialize_ShouldDeserializeToSameObject()
    {
        // Arrange
        var secrets = new Secrets { LykosAccount = new LykosAccountV1Tokens("123", "456"), };

        // Act
        var serialized = GlobalEncryptedSerializer.Serialize(secrets);
        var deserialized = GlobalEncryptedSerializer.Deserialize<Secrets>(serialized);

        // Assert
        Assert.AreEqual(secrets, deserialized);
    }

    [TestMethod]
    public void SerializeV1_ShouldDeserializeToSameObject()
    {
        // Arrange
        var secrets = new Secrets { LykosAccount = new LykosAccountV1Tokens("123", "456"), };

        // Act
        var serialized = GlobalEncryptedSerializer.Serialize(secrets, GlobalEncryptedSerializer.KeyInfoV1);
        var deserialized = GlobalEncryptedSerializer.Deserialize<Secrets>(serialized);

        // Assert
        Assert.AreEqual(secrets, deserialized);
    }

    [TestMethod]
    public void SerializeV2_ShouldDeserializeToSameObject()
    {
        // Arrange
        var secrets = new Secrets { LykosAccount = new LykosAccountV1Tokens("123", "456"), };

        // Act
        var serialized = GlobalEncryptedSerializer.Serialize(secrets, GlobalEncryptedSerializer.KeyInfoV2);
        var deserialized = GlobalEncryptedSerializer.Deserialize<Secrets>(serialized);

        // Assert
        Assert.AreEqual(secrets, deserialized);
    }

    [TestMethod]
    public void SerializeWithNonDefaultKeyInfo_ShouldDeserializeToSameObject()
    {
        // Arrange
        var secrets = new Secrets { LykosAccount = new LykosAccountV1Tokens("123", "456"), };

        // Act
        var serialized = GlobalEncryptedSerializer.Serialize(
            secrets,
            GlobalEncryptedSerializer.KeyInfoV2 with
            {
                Iterations = GlobalEncryptedSerializer.KeyInfoV2.Iterations + 10,
            }
        );
        var deserialized = GlobalEncryptedSerializer.Deserialize<Secrets>(serialized);

        // Assert
        Assert.AreEqual(secrets, deserialized);
    }

    [TestMethod]
    public void EncryptAndDecryptBytesWithKeyInfoV2_ShouldReturnSameBytes()
    {
        // Arrange
        var data = "hello"u8.ToArray();
        var keyInfo = GlobalEncryptedSerializer.KeyInfoV2;
        var password = GetSecureString("password");

        // Act
        var (encrypted, salt) = GlobalEncryptedSerializer.EncryptBytes(data, password, keyInfo);
        var decrypted = GlobalEncryptedSerializer.DecryptBytes(encrypted, salt, password, keyInfo);

        // Assert
        CollectionAssert.AreEqual(data, decrypted);
    }

    [TestMethod]
    public void EncryptAndDecryptBytesWithKeyInfoV2_DifferentPassword_ShouldFail()
    {
        // Arrange
        var data = "hello"u8.ToArray();
        var keyInfo = GlobalEncryptedSerializer.KeyInfoV2;
        var encryptPassword = GetSecureString("password");
        var decryptPassword = GetSecureString("a_different_password");

        // Act
        var (encrypted, salt) = GlobalEncryptedSerializer.EncryptBytes(data, encryptPassword, keyInfo);

        // Assert
        Assert.ThrowsException<CryptographicException>(
            () => GlobalEncryptedSerializer.DecryptBytes(encrypted, salt, decryptPassword, keyInfo)
        );
    }

    private static SecureString GetSecureString(string value)
    {
        var secureString = new SecureString();
        foreach (var c in value)
        {
            secureString.AppendChar(c);
        }
        return secureString;
    }
}
