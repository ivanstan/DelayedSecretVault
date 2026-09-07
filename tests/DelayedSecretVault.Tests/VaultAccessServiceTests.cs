using DelayedSecretVault.Models;
using DelayedSecretVault.Services;
using DelayedSecretVault.Tests.Fakes;

namespace DelayedSecretVault.Tests;

public sealed class VaultAccessServiceTests
{
    private static (VaultAccessService Service, FakeClock Clock, ControllableDelayProvider Delay, AppSettings Settings)
        Create(AppSettings? settings = null)
    {
        var clock = new FakeClock();
        var delay = new ControllableDelayProvider();
        var appSettings = settings ?? AppSettings.CreateDefaults();
        var service = new VaultAccessService(clock, delay, () => appSettings);
        return (service, clock, delay, appSettings);
    }

    [Fact]
    public void Initializes_Locked()
    {
        var (service, _, _, _) = Create();
        Assert.Equal(VaultState.Locked, service.State);
    }

    [Fact]
    public void Locked_Request_Goes_Waiting()
    {
        var (service, _, delay, _) = Create();
        Assert.True(service.RequestAccess());
        Assert.Equal(VaultState.Waiting, service.State);
        Assert.Equal(1, delay.StartedCount);
    }

    [Fact]
    public void Second_Request_While_Waiting_Does_Not_Create_Another_Countdown()
    {
        var (service, _, delay, _) = Create();
        Assert.True(service.RequestAccess());
        Assert.False(service.RequestAccess());
        Assert.Equal(VaultState.Waiting, service.State);
        Assert.Equal(1, delay.StartedCount);
        Assert.Equal(1, delay.PendingCount);
    }

    [Fact]
    public void Waiting_Cancel_Goes_Locked()
    {
        var (service, _, _, _) = Create();
        service.RequestAccess();
        Assert.True(service.CancelRequest());
        Assert.Equal(VaultState.Locked, service.State);
    }

    [Fact]
    public async Task Countdown_Completion_Goes_Confirmation()
    {
        var (service, _, delay, _) = Create();
        service.RequestAccess();
        delay.CompleteNext();
        await WaitForAsync(() => service.State == VaultState.Confirmation);
        Assert.Equal(VaultState.Confirmation, service.State);
    }

    [Fact]
    public async Task Confirmation_Cancel_Goes_Locked()
    {
        var (service, _, delay, _) = Create();
        service.RequestAccess();
        delay.CompleteNext();
        await WaitForAsync(() => service.State == VaultState.Confirmation);
        Assert.True(service.CancelConfirmation());
        Assert.Equal(VaultState.Locked, service.State);
    }

    [Fact]
    public async Task Confirmation_Unlock_Goes_Unlocked()
    {
        var (service, _, delay, _) = Create();
        service.RequestAccess();
        delay.CompleteNext();
        await WaitForAsync(() => service.State == VaultState.Confirmation);
        Assert.True(service.ConfirmUnlock());
        Assert.Equal(VaultState.Unlocked, service.State);
        Assert.Equal(1, delay.StartedCount);
    }

    [Fact]
    public async Task Unlocked_Stays_Unlocked_Until_LockNow()
    {
        var (service, _, delay, _) = Create();
        await UnlockAsync(service, delay);
        Assert.Equal(VaultState.Unlocked, service.State);
        Assert.Equal(1, delay.StartedCount);
        Assert.Equal(0, delay.PendingCount);

        Assert.True(service.LockNow());
        Assert.Equal(VaultState.Locked, service.State);
    }

    [Fact]
    public async Task LockNow_Goes_Locked()
    {
        var (service, _, delay, _) = Create();
        await UnlockAsync(service, delay);
        Assert.True(service.LockNow());
        Assert.Equal(VaultState.Locked, service.State);
    }

    [Fact]
    public async Task Application_Recreation_Produces_Locked()
    {
        var (service, _, delay, settings) = Create();
        await UnlockAsync(service, delay);
        Assert.Equal(VaultState.Unlocked, service.State);
        service.Dispose();

        var recreated = new VaultAccessService(new FakeClock(), new ControllableDelayProvider(), () => settings);
        Assert.Equal(VaultState.Locked, recreated.State);
    }

    [Fact]
    public async Task Protected_Capabilities_Require_Unlocked()
    {
        var (service, _, delay, _) = Create();
        Assert.False(service.CanDecrypt);
        Assert.False(service.CanMutateSecrets);
        Assert.False(service.CanChangeProtectedSettings);

        service.RequestAccess();
        Assert.False(service.CanDecrypt);

        delay.CompleteNext();
        await WaitForAsync(() => service.State == VaultState.Confirmation);
        Assert.False(service.CanDecrypt);

        service.ConfirmUnlock();
        Assert.True(service.CanDecrypt);
        Assert.True(service.CanMutateSecrets);
        Assert.True(service.CanChangeProtectedSettings);
    }

    private static async Task UnlockAsync(VaultAccessService service, ControllableDelayProvider delay)
    {
        service.RequestAccess();
        delay.CompleteNext();
        await WaitForAsync(() => service.State == VaultState.Confirmation);
        service.ConfirmUnlock();
        Assert.Equal(VaultState.Unlocked, service.State);
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
