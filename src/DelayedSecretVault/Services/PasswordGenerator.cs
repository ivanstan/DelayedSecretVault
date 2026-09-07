using System.Security.Cryptography;

namespace DelayedSecretVault.Services;

public static class PasswordGenerator
{
    private const string Lower = "abcdefghijklmnopqrstuvwxyz";
    private const string Upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Digits = "0123456789";
    private const string Alphabet = Lower + Upper + Digits;

    /// <summary>
    /// Generates a strong alphanumeric password (no symbols).
    /// </summary>
    public static string Generate(int length = 24)
    {
        if (length < 12)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be at least 12.");
        }

        var chars = new char[length];
        chars[0] = Lower[RandomNumberGenerator.GetInt32(Lower.Length)];
        chars[1] = Upper[RandomNumberGenerator.GetInt32(Upper.Length)];
        chars[2] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];

        for (var i = 3; i < length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        // Fisher–Yates shuffle so required classes are not fixed at the start.
        for (var i = length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
