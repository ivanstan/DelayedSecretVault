using System.Windows;
using DelayedSecretVault.Services;

namespace DelayedSecretVault.Dialogs;

public partial class SecretEditorDialog : Window
{
    public enum EditorMode
    {
        Add,
        Rename,
        ReplaceValue
    }

    private readonly EditorMode _mode;

    public SecretEditorDialog(EditorMode mode, string? initialName = null)
    {
        InitializeComponent();
        _mode = mode;
        NameBox.Text = initialName ?? string.Empty;

        switch (mode)
        {
            case EditorMode.Add:
                Title = "Add secret";
                TitleText.Text = "Add secret";
                break;
            case EditorMode.Rename:
                Title = "Rename secret";
                TitleText.Text = "Rename secret";
                SecretSection.Visibility = Visibility.Collapsed;
                break;
            case EditorMode.ReplaceValue:
                Title = "Replace secret value";
                TitleText.Text = "Replace secret value";
                NameBox.IsEnabled = false;
                SecretLabel.Text = "New secret";
                break;
        }

        UpdateLengthText();
    }

    public string NameText => NameBox.Text;
    public string SecretText => SecretBox.Text;

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        SecretBox.Text = PasswordGenerator.Generate();
        SecretBox.CaretIndex = SecretBox.Text.Length;
        SecretBox.Focus();
    }

    private void SecretBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => UpdateLengthText();

    private void UpdateLengthText()
    {
        var count = SecretBox.Text.Length;
        LengthText.Text = count == 1 ? "1 character" : $"{count} characters";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;

        if (_mode != EditorMode.ReplaceValue && string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ErrorText.Text = "Name is required.";
            return;
        }

        if (_mode != EditorMode.Rename && string.IsNullOrEmpty(SecretBox.Text))
        {
            ErrorText.Text = "Secret is required.";
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
