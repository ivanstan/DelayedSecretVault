namespace DelayedSecretVault.Models;

public enum VaultState
{
    Locked,
    Waiting,
    Confirmation,
    Unlocked
}
