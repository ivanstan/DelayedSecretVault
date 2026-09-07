namespace DelayedSecretVault.Services;

public interface IDelayProvider
{
    Task Delay(TimeSpan duration, CancellationToken cancellationToken);
}

public sealed class SystemDelayProvider : IDelayProvider
{
    public Task Delay(TimeSpan duration, CancellationToken cancellationToken)
        => Task.Delay(duration, cancellationToken);
}
