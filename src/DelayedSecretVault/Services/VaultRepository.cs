using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DelayedSecretVault.Models;

namespace DelayedSecretVault.Services;

public interface IVaultRepository
{
    VaultData Load();
    void Save(VaultData vault);
    IReadOnlyList<(Guid Id, string Name)> ListNames();
    void Add(SecretEntry entry);
    void Update(SecretEntry entry);
    void Delete(Guid id);
    SecretEntry? Get(Guid id);
}

public sealed class VaultRepository : IVaultRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _vaultPath;
    private readonly object _sync = new();

    public VaultRepository(string? storageDirectory = null)
    {
        var dir = storageDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DelayedSecretVault");
        Directory.CreateDirectory(dir);
        _vaultPath = Path.Combine(dir, "vault.json");
    }

    public string VaultPath => _vaultPath;

    public VaultData Load()
    {
        lock (_sync)
        {
            return LoadUnlocked();
        }
    }

    public void Save(VaultData vault)
    {
        ArgumentNullException.ThrowIfNull(vault);
        lock (_sync)
        {
            WriteAtomic(vault);
        }
    }

    public IReadOnlyList<(Guid Id, string Name)> ListNames()
    {
        var vault = Load();
        return vault.Entries
            .Select(e => (e.Id, e.Name))
            .ToList();
    }

    public SecretEntry? Get(Guid id)
    {
        var vault = Load();
        var entry = vault.Entries.FirstOrDefault(e => e.Id == id);
        if (entry is null)
        {
            return null;
        }

        return new SecretEntry
        {
            Id = entry.Id,
            Name = entry.Name,
            Value = entry.Value.ToArray()
        };
    }

    public void Add(SecretEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_sync)
        {
            var vault = LoadUnlocked();
            if (vault.Entries.Any(e => e.Id == entry.Id))
            {
                throw new InvalidOperationException("An entry with this id already exists.");
            }

            vault.Entries.Add(new SecretEntry
            {
                Id = entry.Id,
                Name = entry.Name,
                Value = entry.Value.ToArray()
            });
            WriteAtomic(vault);
        }
    }

    public void Update(SecretEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_sync)
        {
            var vault = LoadUnlocked();
            var existing = vault.Entries.FirstOrDefault(e => e.Id == entry.Id)
                ?? throw new InvalidOperationException("Entry not found.");
            existing.Name = entry.Name;
            existing.Value = entry.Value.ToArray();
            WriteAtomic(vault);
        }
    }

    public void Delete(Guid id)
    {
        lock (_sync)
        {
            var vault = LoadUnlocked();
            var removed = vault.Entries.RemoveAll(e => e.Id == id);
            if (removed == 0)
            {
                throw new InvalidOperationException("Entry not found.");
            }

            WriteAtomic(vault);
        }
    }

    private VaultData LoadUnlocked()
    {
        if (!File.Exists(_vaultPath))
        {
            return new VaultData();
        }

        var json = File.ReadAllText(_vaultPath);
        var dto = JsonSerializer.Deserialize<VaultFileDto>(json, JsonOptions);
        if (dto is null)
        {
            return new VaultData();
        }

        return new VaultData
        {
            Version = dto.Version <= 0 ? 1 : dto.Version,
            Entries = dto.Entries.Select(e => new SecretEntry
            {
                Id = e.Id,
                Name = e.Name ?? string.Empty,
                Value = string.IsNullOrEmpty(e.Value)
                    ? Array.Empty<byte>()
                    : Convert.FromBase64String(e.Value)
            }).ToList()
        };
    }

    private void WriteAtomic(VaultData vault)
    {
        var dto = new VaultFileDto
        {
            Version = vault.Version <= 0 ? 1 : vault.Version,
            Entries = vault.Entries.Select(e => new VaultEntryDto
            {
                Id = e.Id,
                Name = e.Name,
                Value = Convert.ToBase64String(e.Value)
            }).ToList()
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var directory = Path.GetDirectoryName(_vaultPath)!;
        Directory.CreateDirectory(directory);
        var tempPath = _vaultPath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Copy(tempPath, _vaultPath, overwrite: true);
        File.Delete(tempPath);
    }

    private sealed class VaultFileDto
    {
        public int Version { get; set; } = 1;
        public List<VaultEntryDto> Entries { get; set; } = new();
    }

    private sealed class VaultEntryDto
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }

        /// <summary>Base64-encoded DPAPI ciphertext (not plaintext).</summary>
        public string? Value { get; set; }
    }
}
