namespace DelayedSecretVault.Models;

public sealed class SecretEntry
{
    public Guid Id { get; init; }
    public string Name { get; set; } = string.Empty;

    /// <summary>DPAPI ciphertext bytes. Never plaintext.</summary>
    public byte[] Value { get; set; } = Array.Empty<byte>();
}
