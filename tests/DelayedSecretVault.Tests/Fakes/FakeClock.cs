using DelayedSecretVault.Services;

namespace DelayedSecretVault.Tests.Fakes;

public sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset? start = null)
    {
        UtcNow = start ?? new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    }

    public DateTimeOffset UtcNow { get; private set; }

    public void Advance(TimeSpan duration) => UtcNow += duration;
}

public sealed class ControllableDelayProvider : IDelayProvider
{
    private readonly object _sync = new();
    private readonly Queue<DelayRequest> _pending = new();
    private int _startedCount;

    public int StartedCount
    {
        get
        {
            lock (_sync)
            {
                return _startedCount;
            }
        }
    }

    public int PendingCount
    {
        get
        {
            lock (_sync)
            {
                return _pending.Count;
            }
        }
    }

    public Task Delay(TimeSpan duration, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

        lock (_sync)
        {
            _startedCount++;
            _pending.Enqueue(new DelayRequest(duration, tcs, registration));
        }

        return tcs.Task;
    }

    public void CompleteNext()
    {
        DelayRequest request;
        lock (_sync)
        {
            if (_pending.Count == 0)
            {
                throw new InvalidOperationException("No pending delays.");
            }

            request = _pending.Dequeue();
        }

        request.Registration.Dispose();
        request.Completion.TrySetResult();
    }

    public void CompleteAll()
    {
        while (true)
        {
            DelayRequest? request = null;
            lock (_sync)
            {
                if (_pending.Count == 0)
                {
                    break;
                }

                request = _pending.Dequeue();
            }

            request.Registration.Dispose();
            request.Completion.TrySetResult();
        }
    }

    private sealed record DelayRequest(
        TimeSpan Duration,
        TaskCompletionSource Completion,
        CancellationTokenRegistration Registration);
}
