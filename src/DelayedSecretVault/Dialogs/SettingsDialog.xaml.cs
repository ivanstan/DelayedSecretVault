using System.Globalization;
using System.Windows;
using DelayedSecretVault.Models;

namespace DelayedSecretVault.Dialogs;

public partial class SettingsDialog : Window
{
    public SettingsDialog(AppSettings settings)
    {
        InitializeComponent();

        var unit = AppSettings.AccessDelayUsesSeconds ? "seconds" : "minutes";
        var minimum = AppSettings.DescribeDuration(AppSettings.MinimumVaultAccessDelay);
        AccessDelayLabel.Text = $"Vault access delay ({unit}, minimum {minimum})";
        AccessDelayHint.Text = $"Delay can only be extended above {minimum}, not lowered.";

        AccessDelayBox.Text = FormatAccessDelayValue(settings.VaultAccessDelay);
        RevealDurationBox.Text = settings.SecretRevealDuration.TotalSeconds.ToString(CultureInfo.InvariantCulture);
        ClipboardDurationBox.Text = settings.ClipboardClearDuration.TotalSeconds.ToString(CultureInfo.InvariantCulture);
    }

    public AppSettings? Result { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;

        if (!TryParseNonNegative(AccessDelayBox.Text, out var accessValue) ||
            !TryParseNonNegative(RevealDurationBox.Text, out var revealSeconds) ||
            !TryParseNonNegative(ClipboardDurationBox.Text, out var clipboardSeconds))
        {
            ErrorText.Text = "Enter valid non-negative numbers.";
            return;
        }

        var accessDelay = AppSettings.AccessDelayUsesSeconds
            ? TimeSpan.FromSeconds(accessValue)
            : TimeSpan.FromMinutes(accessValue);

        if (accessDelay < AppSettings.MinimumVaultAccessDelay)
        {
            ErrorText.Text =
                $"Vault access delay must be at least {AppSettings.DescribeDuration(AppSettings.MinimumVaultAccessDelay)}.";
            return;
        }

        Result = new AppSettings
        {
            VaultAccessDelay = accessDelay,
            SecretRevealDuration = TimeSpan.FromSeconds(revealSeconds),
            ClipboardClearDuration = TimeSpan.FromSeconds(clipboardSeconds)
        };
        Result.EnforceInvariants();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static string FormatAccessDelayValue(TimeSpan delay)
    {
        var value = AppSettings.AccessDelayUsesSeconds
            ? delay.TotalSeconds
            : delay.TotalMinutes;
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static bool TryParseNonNegative(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               && value >= 0
               && !double.IsNaN(value)
               && !double.IsInfinity(value);
    }
}
