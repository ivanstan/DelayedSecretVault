namespace DelayedSecretVault.Services;

public interface ISystemClipboard
{
    string? GetText();
    void SetText(string text);
    void Clear();
}

public sealed class WpfClipboard : ISystemClipboard
{
    public string? GetText()
    {
        return System.Windows.Clipboard.ContainsText()
            ? System.Windows.Clipboard.GetText()
            : null;
    }

    public void SetText(string text) => System.Windows.Clipboard.SetText(text);

    public void Clear() => System.Windows.Clipboard.Clear();
}

public sealed class ClipboardService : IDisposable
{
    private readonly ISystemClipboard _clipboard;
    private readonly IDelayProvider _delayProvider;
    private readonly object _sync = new();
    private CancellationTokenSource? _clearCts;
    private string? _copiedValue;
    private bool _disposed;

    public ClipboardService(ISystemClipboard clipboard, IDelayProvider delayProvider)
    {
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _delayProvider = delayProvider ?? throw new ArgumentNullException(nameof(delayProvider));
    }

    public void CopySecret(string value, TimeSpan clearAfter)
    {
        ArgumentNullException.ThrowIfNull(value);
        ThrowIfDisposed();

        CancellationTokenSource cts;
        lock (_sync)
        {
            CancelClearInternal();
            _clipboard.SetText(value);
            _copiedValue = value;
            _clearCts = new CancellationTokenSource();
            cts = _clearCts;
        }

        _ = ClearAfterAsync(clearAfter, cts.Token);
    }

    public void ClearIfOurs()
    {
        lock (_sync)
        {
            ClearIfOursUnlocked();
            CancelClearInternal();
            _copiedValue = null;
        }
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
            ClearIfOursUnlocked();
            CancelClearInternal();
            _copiedValue = null;
        }
    }

    private async Task ClearAfterAsync(TimeSpan delay, CancellationToken token)
    {
        try
        {
            await _delayProvider.Delay(delay, token).ConfigureAwait(false);
            lock (_sync)
            {
                if (_disposed || token.IsCancellationRequested)
                {
                    return;
                }

                ClearIfOursUnlocked();
                _copiedValue = null;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ClearIfOursUnlocked()
    {
        if (_copiedValue is null)
        {
            return;
        }

        try
        {
            var current = _clipboard.GetText();
            if (current == _copiedValue)
            {
                _clipboard.Clear();
            }
        }
        catch
        {
            // Clipboard may be unavailable; ignore.
        }
    }

    private void CancelClearInternal()
    {
        try
        {
            _clearCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _clearCts?.Dispose();
        _clearCts = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ClipboardService));
        }
    }
}
