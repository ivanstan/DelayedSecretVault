using System.Windows;

namespace DelayedSecretVault.Dialogs;

public partial class DeleteConfirmDialog : Window
{
    private readonly string _expectedName;

    public DeleteConfirmDialog(string secretName)
    {
        InitializeComponent();
        _expectedName = secretName;
        PromptText.Text = $"Delete \"{secretName}\"?";
    }

    public string TypedName => ConfirmBox.Text;

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (!string.Equals(ConfirmBox.Text, _expectedName, StringComparison.Ordinal))
        {
            ErrorText.Text = "Confirmation name does not match.";
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
