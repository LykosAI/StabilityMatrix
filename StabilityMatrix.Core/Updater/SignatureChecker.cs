using System.Text;
using NSec.Cryptography;

namespace StabilityMatrix.Core.Updater;

public class SignatureChecker
{
    private static readonly SignatureAlgorithm Algorithm = SignatureAlgorithm.Ed25519;
    private const string UpdatePublicKey = 
        "-----BEGIN PUBLIC KEY-----\n" +
        "MCowBQYDK2VwAyEAqYXhKG1b0iOMnAZGBSBdFlFEWpFBIbIPQk0TtyE2SfI=\n" +
        "-----END PUBLIC KEY-----\n";

    private readonly PublicKey publicKey;
    
    /// <summary>
    /// Initializes a new instance of SignatureChecker.
    /// </summary>
    /// <param name="publicKeyPkix">Pkix format public key. Defaults to update verification key.</param>
    public SignatureChecker(string? publicKeyPkix = null)
    {
        publicKey = PublicKey.Import(
            Algorithm, 
            Encoding.ASCII.GetBytes(publicKeyPkix ?? UpdatePublicKey),
            KeyBlobFormat.PkixPublicKeyText);
    }
    
    /// <summary>
    /// Verifies the signature of provided data.
    /// </summary>
    /// <param name="data">Data to verify</param>
    /// <param name="signature">Signature in base64 encoding</param>
    /// <returns>True if verified</returns>
    public bool Verify(string data, string signature)
    {
        var signatureBytes = Convert.FromBase64String(signature);
        return Algorithm.Verify(publicKey, Encoding.UTF8.GetBytes(data), signatureBytes);
    }
    
    /// <summary>
    /// Verifies the signature of provided data.
    /// </summary>
    /// <param name="data">Data to verify</param>
    /// <param name="signature">Signature in base64 encoding</param>
    /// <returns>True if verified</returns>
    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        return Algorithm.Verify(publicKey, data, signature);
    }
}
