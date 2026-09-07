using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using DelayedSecretVault.Models;
using DelayedSecretVault.Services;

namespace DelayedSecretVault.ViewModels;

public sealed class SecretListItem : INotifyPropertyChanged
{
    private string? _revealedValue;
    private bool _isRevealed;

    public Guid Id { get; init; }
    public string Name { get; set; } = string.Empty;

    public bool IsRevealed
    {
        get => _isRevealed;
        set => SetField(ref _isRevealed, value);
    }

    public string? RevealedValue
    {
        get => _revealedValue;
        set => SetField(ref _revealedValue, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public void ClearReveal()
    {
        RevealedValue = null;
        IsRevealed = false;
    }
}

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly VaultAccessService _access;
    private readonly IVaultRepository _repository;
    private readonly ISecretEncryptionService _encryption;
    private readonly ISettingsService _settingsService;
    private readonly ClipboardService _clipboard;
    private readonly DispatcherTimer _uiTimer;
    private readonly Dispatcher _dispatcher;

    private AppSettings _settings;
    private VaultState _state;
    private string _statusText = "LOCKED";
    private string _remainingText = string.Empty;
    private string? _selectedRevealPlaintext;
    private Guid? _revealedEntryId;
    private CancellationTokenSource? _revealCts;
    private SecretListItem? _selectedSecret;
    private bool _disposed;

    public MainViewModel(
        VaultAccessService access,
        IVaultRepository repository,
        ISecretEncryptionService encryption,
        ISettingsService settingsService,
        ClipboardService clipboard,
        Dispatcher? dispatcher = null)
    {
        _access = access;
        _repository = repository;
        _encryption = encryption;
        _settingsService = settingsService;
        _clipboard = clipboard;
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
        _settings = _settingsService.Load();
        _state = _access.State;

        Secrets = new ObservableCollection<SecretListItem>();
        RefreshSecretList();
        UpdateStatusPresentation();

        _access.StateChanged += OnAccessStateChanged;
        _uiTimer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _uiTimer.Tick += (_, _) => OnUiTick();
        _uiTimer.Start();
    }

    public ObservableCollection<SecretListItem> Secrets { get; }

    public VaultState State
    {
        get => _state;
        private set
        {
            if (_state == value)
            {
                return;
            }

            _state = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsLocked));
            OnPropertyChanged(nameof(IsWaiting));
            OnPropertyChanged(nameof(IsConfirmation));
            OnPropertyChanged(nameof(IsUnlocked));
            UpdateStatusPresentation();
        }
    }

    public bool IsLocked => State == VaultState.Locked;
    public bool IsWaiting => State == VaultState.Waiting;
    public bool IsConfirmation => State == VaultState.Confirmation;
    public bool IsUnlocked => State == VaultState.Unlocked;

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public string RemainingText
    {
        get => _remainingText;
        private set => SetField(ref _remainingText, value);
    }

    public string AccessDelayDescription =>
        $"Access requires a {FormatDuration(_settings.VaultAccessDelay)} waiting period.";

    public int SecretCount => Secrets.Count;

    public SecretListItem? SelectedSecret
    {
        get => _selectedSecret;
        set
        {
            if (SetField(ref _selectedSecret, value))
            {
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public bool HasSelection => SelectedSecret is not null;

    public AppSettings CurrentSettings => _settings;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RequestAccess()
    {
        _access.RequestAccess();
        RefreshFromAccess();
    }

    public void CancelRequest()
    {
        _access.CancelRequest();
        RefreshFromAccess();
    }

    public void CancelConfirmation()
    {
        _access.CancelConfirmation();
        RefreshFromAccess();
    }

    public void ConfirmUnlock()
    {
        _access.ConfirmUnlock();
        RefreshFromAccess();
    }

    public void LockNow()
    {
        PerformLockCleanup();
        _access.LockNow();
        RefreshFromAccess();
    }

    public string? TryRevealSelected()
    {
        if (!_access.CanDecrypt || SelectedSecret is null)
        {
            return null;
        }

        var entry = _repository.Get(SelectedSecret.Id);
        if (entry is null)
        {
            return null;
        }

        var plaintext = _encryption.Unprotect(entry.Value);
        BeginReveal(SelectedSecret, plaintext);
        return plaintext;
    }

    public void CopySelected()
    {
        if (!_access.CanDecrypt || SelectedSecret is null)
        {
            return;
        }

        var entry = _repository.Get(SelectedSecret.Id);
        if (entry is null)
        {
            return;
        }

        var plaintext = _encryption.Unprotect(entry.Value);
        _clipboard.CopySecret(plaintext, _settings.ClipboardClearDuration);
    }

    public (bool Ok, string? Error) AddSecret(string name, string secret)
    {
        if (!_access.CanMutateSecrets)
        {
            return (false, "Vault must be unlocked.");
        }

        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return (false, "Name is required.");
        }

        if (string.IsNullOrEmpty(secret))
        {
            return (false, "Secret is required.");
        }

        var duplicate = _repository.ListNames().Any(n =>
            string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase));

        var entry = new SecretEntry
        {
            Id = Guid.NewGuid(),
            Name = name,
            Value = _encryption.Protect(secret)
        };
        _repository.Add(entry);
        RefreshSecretList();
        return (true, duplicate ? "Saved. Warning: another secret uses a similar name." : null);
    }

    public (bool Ok, string? Error) RenameSecret(Guid id, string newName)
    {
        if (!_access.CanMutateSecrets)
        {
            return (false, "Vault must be unlocked.");
        }

        newName = newName.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            return (false, "Name is required.");
        }

        var entry = _repository.Get(id);
        if (entry is null)
        {
            return (false, "Secret not found.");
        }

        entry.Name = newName;
        _repository.Update(entry);
        RefreshSecretList();
        return (true, null);
    }

    public (bool Ok, string? Error) ReplaceSecretValue(Guid id, string secret)
    {
        if (!_access.CanMutateSecrets)
        {
            return (false, "Vault must be unlocked.");
        }

        if (string.IsNullOrEmpty(secret))
        {
            return (false, "Secret is required.");
        }

        var entry = _repository.Get(id);
        if (entry is null)
        {
            return (false, "Secret not found.");
        }

        entry.Value = _encryption.Protect(secret);
        _repository.Update(entry);
        HideReveal();
        return (true, null);
    }

    public (bool Ok, string? Error) DeleteSecret(Guid id, string typedName)
    {
        if (!_access.CanMutateSecrets)
        {
            return (false, "Vault must be unlocked.");
        }

        var entry = _repository.Get(id);
        if (entry is null)
        {
            return (false, "Secret not found.");
        }

        if (!string.Equals(typedName, entry.Name, StringComparison.Ordinal))
        {
            return (false, "Confirmation name does not match.");
        }

        _repository.Delete(id);
        if (SelectedSecret?.Id == id)
        {
            SelectedSecret = null;
        }

        HideReveal();
        RefreshSecretList();
        return (true, null);
    }

    public (bool Ok, string? Error) SaveSettings(AppSettings settings)
    {
        if (!_access.CanChangeProtectedSettings)
        {
            return (false, "Vault must be unlocked to change settings.");
        }

        ArgumentNullException.ThrowIfNull(settings);
        _settingsService.Save(settings);
        _settings = settings.Clone();
        OnPropertyChanged(nameof(AccessDelayDescription));
        OnPropertyChanged(nameof(CurrentSettings));
        return (true, null);
    }

    public void ReloadSettings()
    {
        _settings = _settingsService.Load();
        OnPropertyChanged(nameof(AccessDelayDescription));
        OnPropertyChanged(nameof(CurrentSettings));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _uiTimer.Stop();
        _access.StateChanged -= OnAccessStateChanged;
        PerformLockCleanup();
        _access.Dispose();
        _clipboard.Dispose();
    }

    private void BeginReveal(SecretListItem item, string plaintext)
    {
        HideReveal();
        _revealedEntryId = item.Id;
        _selectedRevealPlaintext = plaintext;
        item.RevealedValue = plaintext;
        item.IsRevealed = true;

        _revealCts = new CancellationTokenSource();
        var token = _revealCts.Token;
        var duration = _settings.SecretRevealDuration;
        _ = HideRevealAfterAsync(duration, token);
    }

    private async Task HideRevealAfterAsync(TimeSpan duration, CancellationToken token)
    {
        try
        {
            await Task.Delay(duration, token).ConfigureAwait(false);
            await _dispatcher.InvokeAsync(HideReveal);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void HideReveal()
    {
        try
        {
            _revealCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _revealCts?.Dispose();
        _revealCts = null;
        _selectedRevealPlaintext = null;
        _revealedEntryId = null;

        foreach (var secret in Secrets)
        {
            secret.ClearReveal();
        }
    }

    private void PerformLockCleanup()
    {
        HideReveal();
        _clipboard.ClearIfOurs();
        SelectedSecret = null;
    }

    private void OnAccessStateChanged(object? sender, EventArgs e)
    {
        if (_dispatcher.CheckAccess())
        {
            RefreshFromAccess();
        }
        else
        {
            _dispatcher.BeginInvoke(RefreshFromAccess);
        }
    }

    private void RefreshFromAccess()
    {
        var previous = State;
        State = _access.State;
        if (previous == VaultState.Unlocked && State == VaultState.Locked)
        {
            PerformLockCleanup();
        }

        UpdateStatusPresentation();
        RefreshSecretList();
    }

    private void OnUiTick()
    {
        UpdateStatusPresentation();
        _access.NotifyTick();
    }

    private void UpdateStatusPresentation()
    {
        switch (_access.State)
        {
            case VaultState.Locked:
                StatusText = "LOCKED";
                RemainingText = string.Empty;
                break;
            case VaultState.Waiting:
                StatusText = "WAITING";
                RemainingText = FormatRemaining(_access.RemainingWait);
                break;
            case VaultState.Confirmation:
                StatusText = "CONFIRMATION REQUIRED";
                RemainingText = string.Empty;
                break;
            case VaultState.Unlocked:
                StatusText = "UNLOCKED";
                RemainingText = string.Empty;
                break;
        }

        OnPropertyChanged(nameof(SecretCount));
    }

    private void RefreshSecretList()
    {
        var selectedId = SelectedSecret?.Id;
        var revealedId = _revealedEntryId;
        var revealedValue = _selectedRevealPlaintext;

        Secrets.Clear();
        foreach (var (id, name) in _repository.ListNames().OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var item = new SecretListItem { Id = id, Name = name };
            if (revealedId == id && revealedValue is not null && _access.CanDecrypt)
            {
                item.RevealedValue = revealedValue;
                item.IsRevealed = true;
            }

            Secrets.Add(item);
        }

        SelectedSecret = selectedId is null
            ? null
            : Secrets.FirstOrDefault(s => s.Id == selectedId);

        OnPropertyChanged(nameof(SecretCount));
    }

    private static string FormatRemaining(TimeSpan? remaining)
    {
        if (remaining is null)
        {
            return string.Empty;
        }

        var value = remaining.Value < TimeSpan.Zero ? TimeSpan.Zero : remaining.Value;
        var totalMinutes = (int)value.TotalMinutes;
        var seconds = value.Seconds;
        return $"{totalMinutes:D2}:{seconds:D2} remaining";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1 && duration.Minutes == 0 && duration.Seconds == 0)
        {
            var hours = (int)duration.TotalHours;
            return hours == 1 ? "1 hour" : $"{hours} hours";
        }

        if (duration.TotalMinutes >= 1 && duration.Seconds == 0)
        {
            var minutes = (int)duration.TotalMinutes;
            return minutes == 1 ? "1 minute" : $"{minutes} minutes";
        }

        var secs = (int)duration.TotalSeconds;
        return secs == 1 ? "1 second" : $"{secs} seconds";
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
