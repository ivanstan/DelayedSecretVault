using DelayedSecretVault.Models;
using DelayedSecretVault.Services;
using DelayedSecretVault.Tests.Fakes;

namespace DelayedSecretVault.Tests;

public sealed class ClipboardServiceTests
{
    [Fact]
    public async Task Cleanup_Does_Not_Delete_Newer_Unrelated_Clipboard_Contents()
    {
        var clipboard = new FakeClipboard();
        var delay = new ControllableDelayProvider();
        var service = new ClipboardService(clipboard, delay);

        service.CopySecret("vault-secret", TimeSpan.FromSeconds(30));
        Assert.Equal("vault-secret", clipboard.Text);

        clipboard.Text = "unrelated-content-from-another-app";
        delay.CompleteNext();
        await Task.Delay(50);

        Assert.Equal("unrelated-content-from-another-app", clipboard.Text);
    }

    [Fact]
    public async Task Cleanup_Clears_Only_When_Clipboard_Still_Has_Copied_Secret()
    {
        var clipboard = new FakeClipboard();
        var delay = new ControllableDelayProvider();
        var service = new ClipboardService(clipboard, delay);

        service.CopySecret("vault-secret", TimeSpan.FromSeconds(30));
        delay.CompleteNext();
        await Task.Delay(50);

        Assert.True(string.IsNullOrEmpty(clipboard.Text));
    }

    [Fact]
    public void ClearIfOurs_Clears_Matching_Content()
    {
        var clipboard = new FakeClipboard();
        var delay = new ControllableDelayProvider();
        var service = new ClipboardService(clipboard, delay);
        service.CopySecret("vault-secret", TimeSpan.FromMinutes(1));
        service.ClearIfOurs();
        Assert.True(string.IsNullOrEmpty(clipboard.Text));
    }

    private sealed class FakeClipboard : ISystemClipboard
    {
        public string? Text { get; set; }

        public string? GetText() => Text;

        public void SetText(string text) => Text = text;

        public void Clear() => Text = null;
    }
}

public sealed class SettingsServiceTests
{
    [Fact]
    public void Missing_File_Returns_Defaults()
    {
        var dir = Path.Combine(Path.GetTempPath(), "DelayedSecretVaultTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var service = new SettingsService(dir);
        var settings = service.Load();

        Assert.Equal(AppSettings.MinimumVaultAccessDelay, settings.VaultAccessDelay);
        Assert.Equal(TimeSpan.FromSeconds(30), settings.SecretRevealDuration);
        Assert.Equal(TimeSpan.FromSeconds(30), settings.ClipboardClearDuration);
        Assert.False(File.Exists(Path.Combine(dir, "settings.json")));
    }

    [Fact]
    public void Save_Then_Load_RoundTrips()
    {
        var dir = Path.Combine(Path.GetTempPath(), "DelayedSecretVaultTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var service = new SettingsService(dir);
        var settings = new AppSettings
        {
            VaultAccessDelay = AppSettings.MinimumVaultAccessDelay + TimeSpan.FromMinutes(30),
            SecretRevealDuration = TimeSpan.FromSeconds(15),
            ClipboardClearDuration = TimeSpan.FromSeconds(20)
        };
        service.Save(settings);
        var loaded = service.Load();
        Assert.Equal(settings.VaultAccessDelay, loaded.VaultAccessDelay);
        Assert.Equal(settings.SecretRevealDuration, loaded.SecretRevealDuration);
        Assert.Equal(settings.ClipboardClearDuration, loaded.ClipboardClearDuration);
    }

    [Fact]
    public void Access_Delay_Below_Minimum_Is_Clamped()
    {
        var dir = Path.Combine(Path.GetTempPath(), "DelayedSecretVaultTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var service = new SettingsService(dir);
        var belowMinimum = TimeSpan.FromTicks(Math.Max(1, AppSettings.MinimumVaultAccessDelay.Ticks / 2));
        service.Save(new AppSettings
        {
            VaultAccessDelay = belowMinimum,
            SecretRevealDuration = TimeSpan.FromSeconds(30),
            ClipboardClearDuration = TimeSpan.FromSeconds(30)
        });

        var loaded = service.Load();
        Assert.Equal(AppSettings.MinimumVaultAccessDelay, loaded.VaultAccessDelay);
    }

    [Fact]
    public void Settings_Json_Contains_No_Access_Session_Fields()
    {
        var dir = Path.Combine(Path.GetTempPath(), "DelayedSecretVaultTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var service = new SettingsService(dir);
        service.Save(AppSettings.CreateDefaults());
        var json = File.ReadAllText(Path.Combine(dir, "settings.json"));
        Assert.DoesNotContain("state", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unlockAt", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requestedAt", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("remaining", json, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class MutationGuardTests
{
    [Fact]
    public async Task Adding_Editing_Deleting_Require_Unlocked()
    {
        var clock = new FakeClock();
        var delay = new ControllableDelayProvider();
        var settings = AppSettings.CreateDefaults();
        var access = new VaultAccessService(clock, delay, () => settings);
        var dir = Path.Combine(Path.GetTempPath(), "DelayedSecretVaultTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var repo = new VaultRepository(dir);
        var encryption = new SecretEncryptionService();

        Assert.False(access.CanMutateSecrets);

        var id = Guid.NewGuid();
        Assert.ThrowsAny<Exception>(() =>
        {
            if (!access.CanMutateSecrets)
            {
                throw new InvalidOperationException("Vault must be unlocked.");
            }

            repo.Add(new SecretEntry { Id = id, Name = "X", Value = encryption.Protect("a") });
        });

        access.RequestAccess();
        delay.CompleteNext();
        await WaitForAsync(() => access.State == VaultState.Confirmation);
        access.ConfirmUnlock();
        Assert.True(access.CanMutateSecrets);

        repo.Add(new SecretEntry { Id = id, Name = "X", Value = encryption.Protect("a") });
        var entry = repo.Get(id)!;
        entry.Name = "Y";
        repo.Update(entry);
        repo.Delete(id);
        Assert.Empty(repo.ListNames());
    }

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var start = Environment.TickCount64;
        while (!condition())
        {
            if (Environment.TickCount64 - start > timeoutMs)
            {
                throw new TimeoutException("Condition was not met in time.");
            }

            await Task.Delay(10);
        }
    }
}
