using DelayedSecretVault.Models;

namespace DelayedSecretVault.Services;

public sealed class VaultAccessService : IDisposable
{
    private readonly IClock _clock;
    private readonly IDelayProvider _delayProvider;
    private readonly Func<AppSettings> _settingsProvider;
    private readonly object _sync = new();

    private CancellationTokenSource? _waitCts;
    private Task? _waitTask;
    private DateTimeOffset? _waitEndsAt;
    private bool _disposed;

    public VaultAccessService(
        IClock clock,
        IDelayProvider delayProvider,
        Func<AppSettings> settingsProvider)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _delayProvider = delayProvider ?? throw new ArgumentNullException(nameof(delayProvider));
        _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        State = VaultState.Locked;
    }

    public VaultState State { get; private set; }

    public event EventHandler? StateChanged;
    public event EventHandler? Tick;

    public bool CanDecrypt => State == VaultState.Unlocked;
    public bool CanMutateSecrets => State == VaultState.Unlocked;
    public bool CanChangeProtectedSettings => State == VaultState.Unlocked;

    public TimeSpan? RemainingWait
    {
        get
        {
            lock (_sync)
            {
                if (State != VaultState.Waiting || _waitEndsAt is null)
                {
                    return null;
                }

                var remaining = _waitEndsAt.Value - _clock.UtcNow;
                return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
            }
        }
    }

    public bool RequestAccess()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (State != VaultState.Locked)
            {
                return false;
            }

            var settings = _settingsProvider();
            var delay = settings.VaultAccessDelay < AppSettings.MinimumVaultAccessDelay
                ? AppSettings.MinimumVaultAccessDelay
                : settings.VaultAccessDelay;
            _waitCts = new CancellationTokenSource();
            var token = _waitCts.Token;
            _waitEndsAt = _clock.UtcNow + delay;
            TransitionTo(VaultState.Waiting);

            _waitTask = RunWaitAsync(delay, token);
            return true;
        }
    }

    public bool CancelRequest()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (State != VaultState.Waiting)
            {
                return false;
            }

            CancelWaitInternal();
            ClearWait();
            TransitionTo(VaultState.Locked);
            return true;
        }
    }

    public bool CancelConfirmation()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (State != VaultState.Confirmation)
            {
                return false;
            }

            ClearWait();
            TransitionTo(VaultState.Locked);
            return true;
        }
    }

    public bool ConfirmUnlock()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (State != VaultState.Confirmation)
            {
                return false;
            }

            ClearWait();
            TransitionTo(VaultState.Unlocked);
            return true;
        }
    }

    public bool LockNow()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (State != VaultState.Unlocked)
            {
                return false;
            }

            TransitionTo(VaultState.Locked);
            return true;
        }
    }

    public void NotifyTick()
    {
        Tick?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CancelWaitInternal();
            ClearWait();
            if (State != VaultState.Locked)
            {
                State = VaultState.Locked;
            }
        }
    }

    private async Task RunWaitAsync(TimeSpan delay, CancellationToken token)
    {
        try
        {
            await _delayProvider.Delay(delay, token).ConfigureAwait(false);
            lock (_sync)
            {
                if (_disposed || token.IsCancellationRequested || State != VaultState.Waiting)
                {
                    return;
                }

                ClearWait();
                TransitionTo(VaultState.Confirmation);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled by CancelRequest / Dispose.
        }
    }

    private void CancelWaitInternal()
    {
        var cts = _waitCts;
        _waitCts = null;
        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        cts?.Dispose();
    }

    private void ClearWait()
    {
        _waitEndsAt = null;
        _waitTask = null;
        _waitCts?.Dispose();
        _waitCts = null;
    }

    private void TransitionTo(VaultState next)
    {
        if (State == next)
        {
            return;
        }

        if (!IsValidTransition(State, next))
        {
            throw new InvalidOperationException($"Invalid vault transition: {State} -> {next}");
        }

        State = next;
        RaiseStateChanged();
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsValidTransition(VaultState from, VaultState to) => (from, to) switch
    {
        (VaultState.Locked, VaultState.Waiting) => true,
        (VaultState.Waiting, VaultState.Locked) => true,
        (VaultState.Waiting, VaultState.Confirmation) => true,
        (VaultState.Confirmation, VaultState.Locked) => true,
        (VaultState.Confirmation, VaultState.Unlocked) => true,
        (VaultState.Unlocked, VaultState.Locked) => true,
        _ => false
    };

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VaultAccessService));
        }
    }
}
