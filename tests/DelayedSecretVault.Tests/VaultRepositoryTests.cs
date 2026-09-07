using System.Text.Json;
using DelayedSecretVault.Models;
using DelayedSecretVault.Services;

namespace DelayedSecretVault.Tests;

public sealed class VaultRepositoryTests
{
    private static string TempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "DelayedSecretVaultTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void Multiple_Secrets_Can_Be_Stored()
    {
        var dir = TempDir();
        var repo = new VaultRepository(dir);
        repo.Add(new SecretEntry { Id = Guid.NewGuid(), Name = "A", Value = new byte[] { 1 } });
        repo.Add(new SecretEntry { Id = Guid.NewGuid(), Name = "B", Value = new byte[] { 2 } });

        var names = repo.ListNames();
        Assert.Equal(2, names.Count);
        Assert.Contains(names, n => n.Name == "A");
        Assert.Contains(names, n => n.Name == "B");
    }

    [Fact]
    public void Names_Can_Be_Listed_Without_Decrypting_Values()
    {
        var dir = TempDir();
        var repo = new VaultRepository(dir);
        var cipher = new byte[] { 9, 9, 9 };
        repo.Add(new SecretEntry { Id = Guid.NewGuid(), Name = "Router Admin", Value = cipher });

        var names = repo.ListNames();
        Assert.Single(names);
        Assert.Equal("Router Admin", names[0].Name);

        var entry = repo.Get(names[0].Id);
        Assert.NotNull(entry);
        Assert.Equal(cipher, entry!.Value);
    }

    [Fact]
    public void Plaintext_Never_Appears_In_Serialized_Vault_Value_Is_Base64_Dpapi_Cipher()
    {
        var dir = TempDir();
        var encryption = new SecretEncryptionService();
        var repo = new VaultRepository(dir);
        const string secret = "SuperSecretValue-DoNotLeak";
        var cipher = encryption.Protect(secret);
        repo.Add(new SecretEntry
        {
            Id = Guid.NewGuid(),
            Name = "AdGuard Admin",
            Value = cipher
        });

        var json = File.ReadAllText(Path.Combine(dir, "vault.json"));
        Assert.DoesNotContain(secret, json);
        Assert.Contains("\"value\"", json);
        Assert.DoesNotContain("encryptedValue", json);
        Assert.DoesNotContain("\"state\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unlock", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("countdown", json, StringComparison.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(json);
        var valueElement = doc.RootElement.GetProperty("entries")[0].GetProperty("value");
        Assert.Equal(JsonValueKind.String, valueElement.ValueKind);
        var stored = Convert.FromBase64String(valueElement.GetString()!);
        Assert.Equal(cipher, stored);
        Assert.Equal(secret, encryption.Unprotect(stored));
    }

    [Fact]
    public void Countdown_And_Unlocked_State_Are_Never_Serialized()
    {
        var dir = TempDir();
        var repo = new VaultRepository(dir);
        repo.Save(new VaultData
        {
            Version = 1,
            Entries =
            {
                new SecretEntry
                {
                    Id = Guid.NewGuid(),
                    Name = "X",
                    Value = new byte[] { 1, 2, 3 }
                }
            }
        });

        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "vault.json")));
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("version", out _));
        Assert.True(root.TryGetProperty("entries", out _));
        Assert.False(root.TryGetProperty("state", out _));
        Assert.False(root.TryGetProperty("vaultState", out _));
        Assert.False(root.TryGetProperty("requestedAt", out _));
        Assert.False(root.TryGetProperty("unlockAt", out _));
        Assert.False(root.TryGetProperty("remaining", out _));
    }
}
