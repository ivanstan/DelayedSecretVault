namespace DelayedSecretVault.Models;

public sealed class AppSettings
{
    /// <summary>
    /// Debug builds use a short floor for local development; Release/prod builds enforce 60 minutes.
    /// </summary>
#if DEBUG
    public static readonly TimeSpan MinimumVaultAccessDelay = TimeSpan.FromSeconds(10);
#else
    public static readonly TimeSpan MinimumVaultAccessDelay = TimeSpan.FromMinutes(60);
#endif

    /// <summary>
    /// When the floor is under one minute (dev), settings UI edits access delay in seconds; otherwise minutes.
    /// </summary>
    public static bool AccessDelayUsesSeconds => MinimumVaultAccessDelay < TimeSpan.FromMinutes(1);

    public TimeSpan VaultAccessDelay { get; set; } = MinimumVaultAccessDelay;
    public TimeSpan SecretRevealDuration { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan ClipboardClearDuration { get; set; } = TimeSpan.FromSeconds(30);

    public static AppSettings CreateDefaults() => new();

    public AppSettings Clone() => new()
    {
        VaultAccessDelay = VaultAccessDelay,
        SecretRevealDuration = SecretRevealDuration,
        ClipboardClearDuration = ClipboardClearDuration
    };

    /// <summary>
    /// Access delay may only be extended above the environment floor; lower values are clamped up.
    /// </summary>
    public void EnforceInvariants()
    {
        if (VaultAccessDelay < MinimumVaultAccessDelay)
        {
            VaultAccessDelay = MinimumVaultAccessDelay;
        }

        if (SecretRevealDuration < TimeSpan.Zero)
        {
            SecretRevealDuration = TimeSpan.Zero;
        }

        if (ClipboardClearDuration < TimeSpan.Zero)
        {
            ClipboardClearDuration = TimeSpan.Zero;
        }
    }

    public static string DescribeDuration(TimeSpan duration)
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

        var secs = (int)Math.Round(duration.TotalSeconds);
        return secs == 1 ? "1 second" : $"{secs} seconds";
    }
}
