using System.Security.Cryptography;
using System.Text;

namespace DelayedSecretVault.Services;

public interface ISecretEncryptionService
{
    byte[] Protect(string plaintext);
    string Unprotect(byte[] ciphertext);
}

public sealed class SecretEncryptionService : ISecretEncryptionService
{
    public byte[] Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        return ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
    }

    public string Unprotect(byte[] ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        var bytes = ProtectedData.Unprotect(ciphertext, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}
