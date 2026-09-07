using System.Windows;
using DelayedSecretVault.Services;
using DelayedSecretVault.ViewModels;

namespace DelayedSecretVault;

public partial class App : Application
{
    private MainViewModel? _viewModel;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var settingsService = new SettingsService();
        var repository = new VaultRepository();
        var encryption = new SecretEncryptionService();
        var clock = new SystemClock();
        var delayProvider = new SystemDelayProvider();
        var access = new VaultAccessService(clock, delayProvider, settingsService.Load);
        var clipboard = new ClipboardService(new WpfClipboard(), delayProvider);

        _viewModel = new MainViewModel(access, repository, encryption, settingsService, clipboard);
        var window = new MainWindow(_viewModel);
        window.Show();
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        _viewModel?.Dispose();
        _viewModel = null;
    }
}
