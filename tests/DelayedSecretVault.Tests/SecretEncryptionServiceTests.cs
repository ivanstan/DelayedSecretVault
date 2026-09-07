using DelayedSecretVault.Services;

namespace DelayedSecretVault.Tests;

public sealed class SecretEncryptionServiceTests
{
    [Fact]
    public void Values_RoundTrip_Through_Dpapi()
    {
        var service = new SecretEncryptionService();
        const string plaintext = "RouterAdmin!234";
        var cipher = service.Protect(plaintext);
        Assert.False(cipher.AsSpan().SequenceEqual(System.Text.Encoding.UTF8.GetBytes(plaintext)));
        var restored = service.Unprotect(cipher);
        Assert.Equal(plaintext, restored);
    }

    [Fact]
    public void Revealing_Secret_A_Does_Not_Require_Decrypting_Secret_B()
    {
        var real = new SecretEncryptionService();
        var spy = new SpyEncryptionService(real);

        var cipherA = spy.Protect("secret-a");
        var cipherB = spy.Protect("secret-b");
        spy.UnprotectCalls.Clear();

        var revealed = spy.Unprotect(cipherA);
        Assert.Equal("secret-a", revealed);
        Assert.Single(spy.UnprotectCalls);
        Assert.Equal(cipherA, spy.UnprotectCalls[0]);
        Assert.DoesNotContain(cipherB, spy.UnprotectCalls);
    }

    private sealed class SpyEncryptionService : ISecretEncryptionService
    {
        private readonly ISecretEncryptionService _inner;

        public SpyEncryptionService(ISecretEncryptionService inner) => _inner = inner;

        public List<byte[]> UnprotectCalls { get; } = new();

        public byte[] Protect(string plaintext) => _inner.Protect(plaintext);

        public string Unprotect(byte[] ciphertext)
        {
            UnprotectCalls.Add(ciphertext);
            return _inner.Unprotect(ciphertext);
        }
    }
}
