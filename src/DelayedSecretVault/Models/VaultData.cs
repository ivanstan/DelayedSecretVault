namespace DelayedSecretVault.Models;

public sealed class VaultData
{
    public int Version { get; set; } = 1;
    public List<SecretEntry> Entries { get; set; } = new();
}
