using System.Windows;
using DelayedSecretVault.Dialogs;
using DelayedSecretVault.ViewModels;

namespace DelayedSecretVault;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void RequestAccess_Click(object sender, RoutedEventArgs e) => _viewModel.RequestAccess();

    private void CancelRequest_Click(object sender, RoutedEventArgs e) => _viewModel.CancelRequest();

    private void CancelConfirmation_Click(object sender, RoutedEventArgs e) => _viewModel.CancelConfirmation();

    private void ConfirmUnlock_Click(object sender, RoutedEventArgs e) => _viewModel.ConfirmUnlock();

    private void LockNow_Click(object sender, RoutedEventArgs e) => _viewModel.LockNow();

    private void Reveal_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedSecret is null)
        {
            MessageBox.Show(this, "Select a secret first.", "Delayed Secret Vault", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _viewModel.TryRevealSelected();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedSecret is null)
        {
            MessageBox.Show(this, "Select a secret first.", "Delayed Secret Vault", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _viewModel.CopySelected();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SecretEditorDialog(SecretEditorDialog.EditorMode.Add) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var (ok, error) = _viewModel.AddSecret(dialog.NameText, dialog.SecretText);
        if (!ok)
        {
            MessageBox.Show(this, error ?? "Could not add secret.", "Delayed Secret Vault", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!string.IsNullOrEmpty(error))
        {
            MessageBox.Show(this, error, "Delayed Secret Vault", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedSecret is null)
        {
            MessageBox.Show(this, "Select a secret first.", "Delayed Secret Vault", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SecretEditorDialog(SecretEditorDialog.EditorMode.Rename, _viewModel.SelectedSecret.Name) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var (ok, error) = _viewModel.RenameSecret(_viewModel.SelectedSecret.Id, dialog.NameText);
        if (!ok)
        {
            MessageBox.Show(this, error ?? "Could not rename secret.", "Delayed Secret Vault", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedSecret is null)
        {
            MessageBox.Show(this, "Select a secret first.", "Delayed Secret Vault", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SecretEditorDialog(SecretEditorDialog.EditorMode.ReplaceValue, _viewModel.SelectedSecret.Name) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var (ok, error) = _viewModel.ReplaceSecretValue(_viewModel.SelectedSecret.Id, dialog.SecretText);
        if (!ok)
        {
            MessageBox.Show(this, error ?? "Could not replace secret.", "Delayed Secret Vault", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedSecret is null)
        {
            MessageBox.Show(this, "Select a secret first.", "Delayed Secret Vault", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new DeleteConfirmDialog(_viewModel.SelectedSecret.Name) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var (ok, error) = _viewModel.DeleteSecret(_viewModel.SelectedSecret.Id, dialog.TypedName);
        if (!ok)
        {
            MessageBox.Show(this, error ?? "Could not delete secret.", "Delayed Secret Vault", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog(_viewModel.CurrentSettings) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null)
        {
            return;
        }

        var (ok, error) = _viewModel.SaveSettings(dialog.Result);
        if (!ok)
        {
            MessageBox.Show(this, error ?? "Could not save settings.", "Delayed Secret Vault", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _viewModel.Dispose();
    }
}
